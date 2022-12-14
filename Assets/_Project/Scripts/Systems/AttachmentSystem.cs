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

        EntityQueryDesc hostQueryDesc = new EntityQueryDesc
        {
            None = new ComponentType[] { typeof(DedicatedSpeakerTag) },
            All = new ComponentType[] { typeof(EmitterHostComponent), typeof(Translation) }
        };

        EntityQuery speakersQuery = GetEntityQuery(speakerQueryDesc);
        EntityQuery hostQuery = GetEntityQuery(hostQueryDesc);


        //----    UPDATE HOST IN-RANGE STATUSES
        NativeArray<PoolingComponent> speakerPool = speakersQuery.ToComponentDataArray<PoolingComponent>(Allocator.TempJob);
        NativeArray<Translation> speakerTranslations = speakersQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle updateHostRangeJob = Entities.WithName("UpdateHostRange").ForEach
        (
            (ref EmitterHostComponent host, in Translation translation) =>
            {
                if (!host._DedicatedSpeaker)
                {
                    // Calculate if host is currently in-range
                    float listenerDistance = math.distance(translation.Value, activationRanges._ListenerPos);
                    bool inListenerRadiusNow = listenerDistance < activationRanges._ListenerRadius;

                    // Update in-range status of host from the listener.
                    if (!inListenerRadiusNow)
                    {
                        host._SpeakerIndex = int.MaxValue;
                        host._InListenerRadius = false;
                        host._SpeakerAttached = false;
                    }
                    else host._InListenerRadius = true;

                    // Unlink hosts outside speaker attachment radius.
                    if (host._SpeakerAttached)
                    {
                        float emitterToSpeakerDist = math.distance(translation.Value, speakerTranslations[host._SpeakerIndex].Value);
                        if (speakerPool[host._SpeakerIndex]._State == PooledState.Pooled || emitterToSpeakerDist > activationRanges._AttachmentRadius)
                        {
                            host._SpeakerIndex = int.MaxValue;
                            host._SpeakerAttached = false;
                            host._NewSpeaker = false;
                        }
                    }
                }
            }
        ).WithDisposeOnCompletion(speakerPool)
        .WithDisposeOnCompletion(speakerTranslations)
        .ScheduleParallel(Dependency);




        //----    DEACTIVATE SPEAKERS WITHOUT ATTACHED HOSTS
        NativeArray<EmitterHostComponent> hostsWithSpeaker = hostQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob);
        JobHandle updateSpeakerPoolJob = Entities.WithName("UpdateSpeakerPool").ForEach
        (
            (ref PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                if (pooling._State == PooledState.Active)
                {
                    for (int e = 0; e < hostsWithSpeaker.Length; e++)
                        if (hostsWithSpeaker[e]._SpeakerIndex == speaker._SpeakerIndex)
                            // Exit job iteration before ending loop / pooling speaker
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
                if (!host._SpeakerAttached && host._InListenerRadius)
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
                        host._SpeakerIndex = closestSpeakerIndex;
                        host._SpeakerAttached = true;
                        host._NewSpeaker = true;
                    }
                }
            }
        ).WithDisposeOnCompletion(inRangeSpeaker)
        .ScheduleParallel(updateSpeakerPoolJob);




        //----     SPAWN A POOLED SPEAKER ON A HOST IF NO NEARBY SPEAKERS WERE FOUND
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
                        if (hosts[e]._InListenerRadius && !hosts[e]._SpeakerAttached)
                        {
                            spawned = true;
                            float3 speakerPos = GetComponent<Translation>(hostEntities[e]).Value;

                            // Update host component with speaker link
                            EmitterHostComponent host = GetComponent<EmitterHostComponent>(hostEntities[e]);
                            host._SpeakerIndex = GetComponent<SpeakerComponent>(speakerEntities[s])._SpeakerIndex;
                            host._SpeakerAttached = true;
                            host._NewSpeaker = true;
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




        NativeArray<EmitterHostComponent> hostsToSitSpeakers = hostQuery.ToComponentDataArray<EmitterHostComponent>(Allocator.TempJob);
        NativeArray<Translation> hostTranslations = hostQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle moveSpeakersJob = Entities.WithName("MoveSpeakers").ForEach
        (
            (ref Translation translation, in PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                // TODO ---- Triangulate attached host positions and move speaker to the centre
                if (pooling._State == PooledState.Active)
                {
                    int hostIndex = -1;
                    int attachedHosts = 0;
                    // Check if any hosts are attached to this speaker
                    for (int e = 0; e < hostsToSitSpeakers.Length; e++)
                        if (hostsToSitSpeakers[e]._SpeakerIndex == speaker._SpeakerIndex)
                        {
                            hostIndex = e;
                            attachedHosts++;
                        }
                    if (attachedHosts == 1 && hostIndex != -1)
                        translation = hostTranslations[hostIndex];
                }
            }
        ).WithDisposeOnCompletion(hostsToSitSpeakers)
        .WithDisposeOnCompletion(hostTranslations)
        .ScheduleParallel(speakerActivation);

        Dependency = moveSpeakersJob;
    }



    private EmitterHostComponent AttachToSpeaker(EmitterHostComponent host, int index)
    {
        host._SpeakerAttached = true;
        host._SpeakerIndex = index;
        host._NewSpeaker = true;
        return host;
    }
}