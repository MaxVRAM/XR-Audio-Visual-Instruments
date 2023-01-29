﻿using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using System;

// https://docs.unity3d.com/Packages/com.unity.entities@0.51/api/

/// <summary>
//     Processes dynamic emitter host <-> speaker link components amd updates entity in-range statuses.
/// <summary>
[UpdateAfter(typeof(DOTS_QuadrantSystem))]
public partial class AttachmentSystem : SystemBase
{
    // private EndSimulationEntityCommandBufferSystem _CommandBufferSystem;

    // protected override void OnCreate()
    // {
    //     base.OnCreate();
    //     _CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    // }


    protected override void OnUpdate()
    {        
        DSPTimerComponent dspTimer = GetSingleton<DSPTimerComponent>();       
        AttachParameterComponent attachParameters = GetSingleton<AttachParameterComponent>();


        // TODO --- BUG.. filtering out dedicated speakers and hosts with dedicated speakers might be the cause of the indexing
        // issue that's making DOTS fail. It also could be the source of the duplication issue... first on the first to check!


        EntityQueryDesc speakerQueryDesc = new()
        {
            All = new ComponentType[] { typeof(SpeakerComponent), typeof(PoolingComponent), typeof(Translation) }
        };
        EntityQuery speakersQuery = GetEntityQuery(speakerQueryDesc);

        EntityQueryDesc hostQueryDesc = new()
        {
            All = new ComponentType[] { typeof(EmitterHostComponent), typeof(Translation), typeof(UsingDynamicSpeakers) }
        };
        EntityQuery hostsQuery = GetEntityQuery(hostQueryDesc);


        //----    UPDATE HOST IN-RANGE STATUSES
        NativeArray<PoolingComponent>.ReadOnly speakerPool = speakersQuery.ToComponentDataArray<PoolingComponent>(Allocator.TempJob).AsReadOnly();
        NativeArray<Translation>.ReadOnly speakerTranslations = speakersQuery.ToComponentDataArray<Translation>(Allocator.TempJob).AsReadOnly();
        JobHandle updateHostRangeJob = Entities.WithName("UpdateHostRange").WithAny<UsingDynamicSpeakers>().ForEach
        (
            (ref EmitterHostComponent host, in Translation translation) =>
            {
                // Calculate if host is currently in-range
                float listenerDistance = math.distance(translation.Value, attachParameters._ListenerPos);
                bool inListenerRadiusNow = listenerDistance < attachParameters._ListenerRadius;

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
                    if (speakerPool[host._SpeakerIndex]._State == PooledState.Pooled ||
                        emitterToSpeakerDist > speakerPool[host._SpeakerIndex]._AttachmentRadius)
                    {
                        host._Connected = false;
                        host._SpeakerIndex = int.MaxValue;
                    }
                }
            }
        ).WithDisposeOnCompletion(speakerPool)
        .WithDisposeOnCompletion(speakerTranslations)
        .ScheduleParallel(Dependency);

        updateHostRangeJob.Complete();



        //----     CALCULATE SPEAKER ATTACHMENT RADIUS, SET MOVE TO AVERAGE POSITION OF ATTACHED HOST, OR POOL IF NO LONGER ATTACHED
        NativeArray<EmitterHostComponent>.ReadOnly hostsToSitSpeakers = hostsQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob).AsReadOnly();
        NativeArray<Translation>.ReadOnly hostTranslations = hostsQuery.ToComponentDataArray<Translation>(Allocator.TempJob).AsReadOnly();
        JobHandle updateSpeakerPoolJob = Entities.WithName("MoveSpeakers").ForEach
        (
            (ref Translation translation, ref PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                float3 hostPosSum = new(0, 0, 0);
                int attachedHosts = 0;
                
                for (int e = 0; e < hostsToSitSpeakers.Length; e++)
                    if (hostsToSitSpeakers[e]._Connected && hostsToSitSpeakers[e]._SpeakerIndex == speaker._SpeakerIndex)
                    {
                        hostPosSum += hostTranslations[e].Value;
                        attachedHosts++;
                    }
                
                if (attachedHosts == 0)
                {
                    pooling._AttachmentRadius = 0.1f;
                    pooling._State = PooledState.Pooled;
                    translation.Value = attachParameters._PooledSpeakerPosition;
                }
                else
                {
                    float3 newPos = translation.Value;
                    float3 targetPos = hostPosSum / attachedHosts;
                    // Snap to position if speaker was previously pooled, if the proposed position is too far away,
                    // or if the speaker maintains attached host count of 1.
                    // NOTE: would be better to test against previously attached host indexes and snap if none are the same.
                    // TODO: check if it's worth retaining array of host indexes on speaker component. 
                    if (pooling._State == PooledState.Pooled || (pooling._AttachedHostCount == 1 && attachedHosts == 1) ||
                            math.distance(translation.Value, targetPos) > pooling._AttachmentRadius * 2)
                        newPos = targetPos;
                    else
                    {
                        newPos.x = newPos.x.Lerp(targetPos.x, attachParameters._TranslationSmoothing, 0.001f);
                        newPos.y = newPos.y.Lerp(targetPos.y, attachParameters._TranslationSmoothing, 0.001f);
                        newPos.z = newPos.z.Lerp(targetPos.z, attachParameters._TranslationSmoothing, 0.001f);
                    }
                    // Update attachment radius based on new position. TODO - check if this is the best place to calculate the radius value.
                    float listenerCircumference = (float)(2 * Math.PI * math.distance(attachParameters._ListenerPos, newPos));
                    pooling._AttachmentRadius = attachParameters._AttachArcDegrees / 360 * listenerCircumference;
                    pooling._State = PooledState.Active;
                    translation.Value = newPos;
                }
                pooling._AttachedHostCount = attachedHosts;
            }
        ).WithDisposeOnCompletion(hostsToSitSpeakers)
        .WithDisposeOnCompletion(hostTranslations)
        .ScheduleParallel(updateHostRangeJob);

        updateSpeakerPoolJob.Complete();



        //----    ATTACH HOST TO ACTIVE IN-RANGE SPEAKER
        NativeArray<Entity>.ReadOnly inRangeSpeaker = speakersQuery.ToEntityArray(Allocator.TempJob).AsReadOnly();
        JobHandle linkToActiveSpeakerJob = Entities.WithName("LinkToActiveSpeaker").WithAny<UsingDynamicSpeakers>().ForEach
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
                        PoolingComponent speaker = GetComponent<PoolingComponent>(inRangeSpeaker[i]);
                        if (speaker._State == PooledState.Active)
                        {
                            float dist = math.distance(translation.Value, GetComponent<Translation>(inRangeSpeaker[i]).Value);
                            if (dist < speaker._AttachmentRadius)
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

        linkToActiveSpeakerJob.Complete();



        //----     SPAWN A POOLED SPEAKER ON A HOST IF NO NEARBY SPEAKERS WERE FOUND
        NativeArray<Entity> hostEntities = hostsQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<Translation>.ReadOnly hostTrans = hostsQuery.ToComponentDataArray<Translation>(Allocator.TempJob).AsReadOnly();
        NativeArray<EmitterHostComponent>.ReadOnly hosts = hostsQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob).AsReadOnly();
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
                            speakerTranslation.Value = hostTrans[e].Value;
                            SetComponent(speakerEntities[s], speakerTranslation);

                            // Set active pooled status and update attachment radius
                            PoolingComponent pooledObj = GetComponent<PoolingComponent>(speakerEntities[s]);
                            float listenerCircumference = (float)(2 * Math.PI * math.distance(attachParameters._ListenerPos, hostTrans[e].Value));
                            pooledObj._AttachmentRadius = attachParameters._AttachArcDegrees / 360 * listenerCircumference;
                            pooledObj._State = PooledState.Active;
                            SetComponent(speakerEntities[s], pooledObj);

                            break;
                        }
                    }
                }
            }
        }).WithDisposeOnCompletion(hostEntities).WithDisposeOnCompletion(hosts).WithDisposeOnCompletion(hostTrans)
        .WithDisposeOnCompletion(speakerEntities).WithDisposeOnCompletion(speakers)
        .Schedule(linkToActiveSpeakerJob);

        speakerActivation.Complete();

        Dependency = speakerActivation;

    }
}