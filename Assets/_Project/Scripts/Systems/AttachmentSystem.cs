using Unity.Entities;
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
    protected override void OnUpdate()
    {
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




        //----    DEACTIVATE SPEAKERS WITHOUT ATTACHED HOSTS
        NativeArray<EmitterHostComponent> hostsWithSpeaker = GetEntityQuery(typeof(EmitterHostComponent)).ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob);
        JobHandle updateSpeakerPoolJob = Entities.WithName("UpdateSpeakerPool").ForEach
        (
            (ref PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                if (pooling._State == PooledState.Active)
                {
                    for (int e = 0; e < hostsWithSpeaker.Length; e++)
                        if (hostsWithSpeaker[e]._SpeakerIndex == speaker._SpeakerIndex)
                            // Exit iteration before applying pooled state if attached speaker matches
                            return;
                    pooling._State = PooledState.Pooled;
                }
            }
        ).WithDisposeOnCompletion(hostsWithSpeaker)
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
                        host._Connected = false;
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
                            float3 speakerPos = GetComponent<Translation>(hostEntities[e]).Value;

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




        EntityQuery hostSitQuery = GetEntityQuery(typeof(EmitterHostComponent),typeof(Translation));
        NativeArray<EmitterHostComponent> hostsToSitSpeakers = hostSitQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob);
        NativeArray<Translation> hostTranslations = hostSitQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle moveSpeakersJob = Entities.WithName("MoveSpeakers").ForEach
        (
            (ref Translation translation, in PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                if (pooling._State == PooledState.Active)
                {
                    int hostIndex = -1;
                    int attachedHosts = 0;
                    // Check if any hosts are attached to this speaker
                    for (int e = 0; e < hostsToSitSpeakers.Length; e++)
                        if (hostsToSitSpeakers[e]._Connected && hostsToSitSpeakers[e]._SpeakerIndex == speaker._SpeakerIndex)
                        {
                            hostIndex = e;
                            attachedHosts++;
                        }
                    // TODO ---- Find centre of all attached emitters
                    if (attachedHosts == 1 && hostIndex != -1)
                        translation = hostTranslations[hostIndex];
                }
            }
        ).WithDisposeOnCompletion(hostsToSitSpeakers)
        .WithDisposeOnCompletion(hostTranslations)
        .ScheduleParallel(speakerActivation);

        Dependency = moveSpeakersJob;
    }
}