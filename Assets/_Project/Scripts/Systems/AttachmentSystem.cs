﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using System;
using Substance.Game;

// https://docs.unity3d.com/Packages/com.unity.entities@0.13/api/

/// <summary>
//     Processes dynamic emitter host <-> speaker link components amd updates entity in-range statuses.
/// <summary>
[UpdateAfter(typeof(DOTS_QuadrantSystem))]
public class AttachmentSystem : SystemBase
{
    // private EndSimulationEntityCommandBufferSystem _CommandBufferSystem;

    // protected override void OnCreate()
    // {
    //     base.OnCreate();
    //     _CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    // }


    protected override void OnUpdate()
    {
        // // Acquire an ECB and convert it to a concurrent one to be able to use it from a parallel job.
        // EntityCommandBuffer.ParallelWriter entityCommandBuffer = _CommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

        
        DSPTimerComponent dspTimer = GetSingleton<DSPTimerComponent>();       
        ActivationRadiusComponent activationRanges = GetSingleton<ActivationRadiusComponent>();

        EntityQueryDesc speakerQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(SpeakerComponent), typeof(PoolingComponent), typeof(Translation) }
        };

        EntityQuery speakersQuery = GetEntityQuery(speakerQueryDesc);


        //----    UPDATE HOST IN-RANGE STATUSES
        NativeArray<PoolingComponent> speakerPool = speakersQuery.ToComponentDataArray<PoolingComponent>(Allocator.TempJob);
        NativeArray<Translation> speakerTranslations = speakersQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle updateHostRangeJob = Entities.WithName("UpdateHostRange").ForEach
        (
            (ref EmitterHostComponent host, in Translation translation) =>
            {        
                if (!host._HasDedicatedSpeaker)
                {
                    // Calculate if host is currently in-range
                    float listenerDistance = math.distance(translation.Value, activationRanges._ListenerPos);
                    bool inListenerRadiusNow = listenerDistance < activationRanges._ListenerRadius;

                    // Update in-range status of host from the listener.
                    if (!inListenerRadiusNow)
                    {
                        host._Connected = false;
                        host._InListenerRadius = false;
                        host._SpeakerIndex = int.MaxValue;
                    }
                    else host._InListenerRadius = true;

                    // Unlink hosts outside speaker attachment radius.
                    if (host._Connected)
                    {
                        float emitterToSpeakerDist = math.distance(translation.Value, speakerTranslations[host._SpeakerIndex].Value);
                        if (speakerPool[host._SpeakerIndex]._State == PooledState.Pooled || emitterToSpeakerDist > activationRanges._AttachmentRadius)
                        {
                            host._Connected = false;
                            host._SpeakerIndex = int.MaxValue;
                        }
                    }
                }
            }
        ).WithDisposeOnCompletion(speakerPool)
        .WithDisposeOnCompletion(speakerTranslations)
        .ScheduleParallel(Dependency);



        //----     SET SPEAKER POSITION TO AVERAGE POSITION OF ATTACHED HOSTS AND POOL DETACHED SPEAKERS
        EntityQuery hostSitQuery = GetEntityQuery(typeof(EmitterHostComponent),typeof(Translation));
        NativeArray<EmitterHostComponent> hostsToSitSpeakers = hostSitQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob);
        NativeArray<Translation> hostTranslations = hostSitQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle updateSpeakerPoolJob = Entities.WithName("MoveSpeakers").ForEach
        (
            (ref Translation translation, ref PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                float xPos = 0;
                float yPos = 0;
                float zPos = 0;
                int attachedHosts = 0;
                
                for (int e = 0; e < hostsToSitSpeakers.Length; e++)
                    if (hostsToSitSpeakers[e]._Connected && hostsToSitSpeakers[e]._SpeakerIndex == speaker._SpeakerIndex)
                    {
                        xPos += hostTranslations[e].Value.x;
                        yPos += hostTranslations[e].Value.y;
                        zPos += hostTranslations[e].Value.z;
                        attachedHosts++;
                    }

                pooling._AttachedHostCount = attachedHosts;
                
                if (attachedHosts == 0)
                    pooling._State = PooledState.Pooled;
                else
                {
                    if (attachedHosts > 1)
                    {
                        xPos /= attachedHosts;
                        yPos /= attachedHosts;
                        zPos /= attachedHosts;
                    }
                    Translation newTranslation = new Translation();
                    newTranslation.Value.x = xPos;
                    newTranslation.Value.y = yPos;
                    newTranslation.Value.z = zPos;
                    translation = newTranslation;
                    pooling._State = PooledState.Active;
                }
            }
        ).WithDisposeOnCompletion(hostsToSitSpeakers)
        .WithDisposeOnCompletion(hostTranslations)
        .ScheduleParallel(updateHostRangeJob);





        //----    ATTACH HOST TO ACTIVE IN-RANGE SPEAKER
        NativeArray<Entity> inRangeSpeaker = speakersQuery.ToEntityArray(Allocator.TempJob);
        JobHandle linkToActiveSpeakerJob = Entities.WithName("LinkToActiveSpeaker").ForEach
        (
            (ref EmitterHostComponent host, in Translation translation) =>
            {
                // Find hosts in listener radius not currently linked to a speaker
                if (!host._Connected && host._InListenerRadius)
                {
                    float closestDist = float.MaxValue;
                    int closestSpeakerIndex = int.MaxValue;
                    // Find closest speaker to the host
                    for (int i = 0; i < inRangeSpeaker.Length; i++)
                    {
                        if (GetComponent<PoolingComponent>(inRangeSpeaker[i])._State == PooledState.Active)
                        {
                            float dist = math.distance(translation.Value, GetComponent<Translation>(inRangeSpeaker[i]).Value);
                            if (dist < activationRanges._AttachmentRadius)
                            {
                                closestDist = dist;
                                closestSpeakerIndex = GetComponent<SpeakerComponent>(inRangeSpeaker[i])._SpeakerIndex;
                            }
                        }
                    }
                    // Attach the host to the nearest valid speaker
                    if (closestSpeakerIndex != int.MaxValue)
                    {
                        host._Connected = true;
                        host._SpeakerIndex = closestSpeakerIndex;
                    }
                }
            }
        ).WithDisposeOnCompletion(inRangeSpeaker)
        .ScheduleParallel(updateSpeakerPoolJob);




        //----     SPAWN A POOLED SPEAKER ON A HOST IF NO NEARBY SPEAKERS WERE FOUND
        EntityQuery hostQuery = GetEntityQuery(typeof(EmitterHostComponent));
        NativeArray<Entity> hostEntities = hostQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<EmitterHostComponent> hosts = hostQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob);
        NativeArray<Entity> speakerEntities = speakersQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<PoolingComponent> speakers = speakersQuery.ToComponentDataArray<PoolingComponent>(Allocator.TempJob);        

        JobHandle speakerActivation = Job.WithName("speakerActivation").WithoutBurst().WithCode(() =>
        {
            // Find a pooled speaker
            for (int s = 0; s < speakers.Length; s++)
            {
                bool spawned = false;
                if (!spawned && speakers[s]._State == PooledState.Pooled)
                {
                    // Find an unlinked host to link with pooled speaker
                    for (int e = 0; e < hosts.Length; e++)
                    {
                        if (hosts[e]._InListenerRadius && !hosts[e]._Connected)
                        {
                            spawned = true;

                            // Update host component with speaker link
                            EmitterHostComponent host = GetComponent<EmitterHostComponent>(hostEntities[e]);
                            host._SpeakerIndex = GetComponent<SpeakerComponent>(speakerEntities[s])._SpeakerIndex;
                            host._Connected = true;
                            SetComponent(hostEntities[e], host);

                            // Update speaker position
                            Translation speakerTranslation = GetComponent<Translation>(speakerEntities[s]);
                            speakerTranslation.Value = GetComponent<Translation>(hostEntities[e]).Value;
                            SetComponent(speakerEntities[s], speakerTranslation);

                            // Update speaker pooled status to active
                            PoolingComponent pooledObj = GetComponent<PoolingComponent>(speakerEntities[s]);
                            pooledObj._State = PooledState.Active;
                            SetComponent(speakerEntities[s], pooledObj);

                            break;
                        }
                    }
                }
            }
        }).WithDisposeOnCompletion(speakerEntities).WithDisposeOnCompletion(speakers)
        .WithDisposeOnCompletion(hostEntities).WithDisposeOnCompletion(hosts)
        .Schedule(linkToActiveSpeakerJob);

        Dependency = speakerActivation;
    }
}