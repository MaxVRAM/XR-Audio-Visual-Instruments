using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using System;
using static UnityEngine.EventSystems.EventTrigger;

// https://docs.unity3d.com/Packages/com.unity.entities@0.51/api/

/// <summary>
//     Processes dynamic emitter host <-> speaker link components amd updates entity in-range statuses.
/// <summary>
[UpdateAfter(typeof(DOTS_QuadrantSystem))]
public partial class AttachmentSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem _CommandBufferSystem;

    protected override void OnCreate()
    {
        base.OnCreate();
        _CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        // Acquire an ECB and convert it to a concurrent one to be able to use it from a parallel job.
        EntityCommandBuffer.ParallelWriter ecb = _CommandBufferSystem.CreateCommandBuffer().AsParallelWriter();


        EntityQueryDesc speakerQueryDesc = new()
        {
            All = new ComponentType[] { typeof(SpeakerIndex), typeof(SpeakerComponent), typeof(Translation) }
        };
        EntityQuery speakersQuery = GetEntityQuery(speakerQueryDesc);

        EntityQueryDesc hostQueryDesc = new()
        {
            All = new ComponentType[] { typeof(HostComponent), typeof(Translation) }
        };
        EntityQuery hostsQuery = GetEntityQuery(hostQueryDesc);

        AudioTimerComponent dspTimer = GetSingleton<AudioTimerComponent>();       
        AllocationParameters attachParameters = GetSingleton<AllocationParameters>();

        //----    UPDATE HOST IN-RANGE STATUSES
        NativeArray<SpeakerComponent> speakerPool = speakersQuery.ToComponentDataArray<SpeakerComponent>(Allocator.TempJob);
        NativeArray<Translation> speakerTranslations = speakersQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

        JobHandle updateHostRangeJob = Entities.WithName("UpdateHostRange").WithReadOnly(speakerPool).WithReadOnly(speakerTranslations).ForEach
        (
            (int entityInQueryIndex, Entity entity, ref HostComponent host, in Translation translation) =>
            {
                // Calculate if host is currently in-range
                float listenerDistance = math.distance(translation.Value, attachParameters._ListenerPos);
                bool inListenerRadiusNow = listenerDistance < attachParameters._ListenerRadius;

                // Update in-range status of host from the listener.
                if (!inListenerRadiusNow)
                {
                    ecb.RemoveComponent<InListenerRadiusTag>(entityInQueryIndex, entity);
                    host._Connected = false;
                    host._InListenerRadius = false;
                    host._SpeakerIndex = int.MaxValue;
                }
                else
                {
                    ecb.AddComponent(entityInQueryIndex, entity, new InListenerRadiusTag());
                    host._InListenerRadius = true;
                }

                // Unlink hosts outside speaker attachment radius.
                if (host._Connected)
                {
                    float emitterToSpeakerDist = math.distance(translation.Value, speakerTranslations[host._SpeakerIndex].Value);
                    if (speakerPool[host._SpeakerIndex]._State == ConnectionState.Disconnected ||
                        emitterToSpeakerDist > speakerPool[host._SpeakerIndex]._AttachmentRadius)
                    {
                        ecb.RemoveComponent<ConnectedTag>(entityInQueryIndex, entity);
                        host._Connected = false;
                        host._SpeakerIndex = int.MaxValue;
                    }
                }
            }
        ).WithDisposeOnCompletion(speakerPool)
        .WithDisposeOnCompletion(speakerTranslations)
        .ScheduleParallel(Dependency);

        updateHostRangeJob.Complete();

        // Make sure that the ECB system knows about our job
        _CommandBufferSystem.AddJobHandleForProducer(updateHostRangeJob);


        //----     CALCULATE SPEAKER ATTACHMENT RADIUS, SET MOVE TO AVERAGE POSITION OF ATTACHED HOST, OR POOL IF NO LONGER ATTACHED
        NativeArray<HostComponent> hostsToSitSpeakers = hostsQuery.ToComponentDataArray<HostComponent>(Allocator.TempJob);
        NativeArray<Translation> hostTranslations = hostsQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle updateSpeakerPoolJob = Entities.WithName("MoveSpeakers").WithReadOnly(hostsToSitSpeakers).WithReadOnly(hostTranslations).ForEach
        (
            (ref Translation translation, ref SpeakerComponent speaker, in SpeakerIndex index) =>
            {
                float3 hostPosSum = new(0, 0, 0);
                int attachedHosts = 0;
                
                for (int e = 0; e < hostsToSitSpeakers.Length; e++)
                {
                    if (hostsToSitSpeakers[e]._Connected && hostsToSitSpeakers[e]._SpeakerIndex == index.Value)
                    {
                        hostPosSum += hostTranslations[e].Value;
                        attachedHosts++;
                    }
                }
                if (attachedHosts == 0)
                {
                    speaker._AttachmentRadius = 0.1f;
                    speaker._State = ConnectionState.Disconnected;
                    translation.Value = attachParameters._DisconnectedPosition;
                }
                else
                {
                    float3 currentPos = translation.Value;
                    float3 targetPos = hostPosSum / attachedHosts;
                    // Snap to position if speaker was previously pooled, if the proposed position is too far away,
                    // or if the speaker maintains attached host count of 1.
                    // NOTE: would be better to test against previously attached host indexes and snap if none are the same.
                    // TODO: check if it's worth retaining array of host indexes on speaker component. 
                    if (speaker._State == ConnectionState.Disconnected || (speaker._AttachedHostCount == 1 && attachedHosts == 1) ||
                            math.distance(translation.Value, targetPos) > speaker._AttachmentRadius * 2)
                        currentPos = targetPos;
                    else
                        math.lerp(currentPos, targetPos, attachParameters._TranslationSmoothing);
                    // Update attachment radius based on new position. TODO - check if this is the best place to calculate the radius value.
                    float listenerCircumference = (float)(2 * Math.PI * math.distance(attachParameters._ListenerPos, currentPos));
                    speaker._AttachmentRadius = attachParameters._LocalisationArcDegrees / 360 * listenerCircumference;
                    speaker._State = ConnectionState.Connected;
                    translation.Value = currentPos;
                }
                speaker._AttachedHostCount = attachedHosts;
            }
        ).WithDisposeOnCompletion(hostsToSitSpeakers)
        .WithDisposeOnCompletion(hostTranslations)
        .ScheduleParallel(updateHostRangeJob);
        updateSpeakerPoolJob.Complete();

        // Make sure that the ECB system knows about our job
        _CommandBufferSystem.AddJobHandleForProducer(updateSpeakerPoolJob);


        //----    ATTACH HOST TO ACTIVE IN-RANGE SPEAKER
        NativeArray<Entity> inRangeSpeaker = speakersQuery.ToEntityArray(Allocator.TempJob);

        JobHandle linkToActiveSpeakerJob = Entities.WithName("LinkToActiveSpeaker").
            WithNone<ConnectedTag>().WithAll<InListenerRadiusTag>().WithReadOnly(inRangeSpeaker).ForEach
        (
            (int entityInQueryIndex, Entity entity, ref HostComponent host, in Translation translation) =>
            {
                if (!host._Connected && host._InListenerRadius)
                {
                    float closestDist = float.MaxValue;
                    int closestSpeakerIndex = int.MaxValue;
                    // Find closest speaker to the host
                    for (int i = 0; i < inRangeSpeaker.Length; i++)
                    {
                        SpeakerComponent speaker = GetComponent<SpeakerComponent>(inRangeSpeaker[i]);
                        if (speaker._State == ConnectionState.Connected)
                        {
                            float dist = math.distance(translation.Value, GetComponent<Translation>(inRangeSpeaker[i]).Value);
                            if (dist < speaker._AttachmentRadius)
                            {
                                closestDist = dist;
                                closestSpeakerIndex = GetComponent<SpeakerIndex>(inRangeSpeaker[i]).Value;
                            }
                        }
                    }
                    // Attach the host to the nearest valid speaker
                    if (closestSpeakerIndex != int.MaxValue)
                    {
                        ecb.AddComponent(entityInQueryIndex, entity, new ConnectedTag());
                        host._Connected = true;
                        host._SpeakerIndex = closestSpeakerIndex;
                    }
                }
            }
        ).WithDisposeOnCompletion(inRangeSpeaker)
        .ScheduleParallel(updateSpeakerPoolJob);

        linkToActiveSpeakerJob.Complete();

        // Make sure that the ECB system knows about our job
        _CommandBufferSystem.AddJobHandleForProducer(linkToActiveSpeakerJob);


        //----     SPAWN A POOLED SPEAKER ON A HOST IF NO NEARBY SPEAKERS WERE FOUND
        NativeArray<Entity> hostEntities = hostsQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<Translation> hostTrans = hostsQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        NativeArray<HostComponent> hosts = hostsQuery.ToComponentDataArray<HostComponent>(Allocator.TempJob);
        NativeArray<Entity> speakerEntities = speakersQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<SpeakerComponent> speakers = speakersQuery.ToComponentDataArray<SpeakerComponent>(Allocator.TempJob);        

        JobHandle speakerActivation = Job.WithName("speakerActivation").WithoutBurst().WithCode(() =>
        {
            // Find a pooled speaker
            for (int s = 0; s < speakers.Length; s++)
            {
                bool spawned = false;
                if (!spawned && speakers[s]._State == ConnectionState.Disconnected)
                {
                    // Find an unlinked host to link with pooled speaker
                    for (int e = 0; e < hosts.Length; e++)
                    {
                        if (!hosts[e]._Connected && hosts[e]._InListenerRadius)
                        {
                            spawned = true;

                            // Update host component with speaker link
                            HostComponent host = GetComponent<HostComponent>(hostEntities[e]);
                            host._SpeakerIndex = GetComponent<SpeakerIndex>(speakerEntities[s]).Value;
                            host._Connected = true;
                            SetComponent(hostEntities[e], host);

                            // Update speaker position
                            Translation speakerTranslation = GetComponent<Translation>(speakerEntities[s]);
                            speakerTranslation.Value = hostTrans[e].Value;
                            SetComponent(speakerEntities[s], speakerTranslation);

                            // Set active pooled status and update attachment radius
                            SpeakerComponent pooledObj = GetComponent<SpeakerComponent>(speakerEntities[s]);
                            float listenerCircumference = (float)(2 * Math.PI * math.distance(attachParameters._ListenerPos, hostTrans[e].Value));
                            pooledObj._AttachmentRadius = attachParameters._LocalisationArcDegrees / 360 * listenerCircumference;
                            pooledObj._State = ConnectionState.Connected;
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

        // Make sure that the ECB system knows about our job
        _CommandBufferSystem.AddJobHandleForProducer(speakerActivation);


        Dependency = speakerActivation;
    }
}
