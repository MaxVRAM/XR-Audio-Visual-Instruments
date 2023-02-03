using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using System;
using static UnityEngine.EventSystems.EventTrigger;
using System.Net;
using UnityEditorInternal;
using System.Numerics;
using Unity.Core;

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
        AttachmentParameters attachParameters = GetSingleton<AttachmentParameters>();




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
                    // TODO: Need to investigate why we put the speaker connection state check here. Might be better not to.
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

        _CommandBufferSystem.AddJobHandleForProducer(updateHostRangeJob);


        //----     CALCULATE SPEAKER ATTACHMENT RADIUS, SET MOVE TO AVERAGE POSITION OF ATTACHED HOST, OR POOL IF NO LONGER ATTACHED
        NativeArray<HostComponent> hostsToSitSpeakers = hostsQuery.ToComponentDataArray<HostComponent>(Allocator.TempJob);
        NativeArray<Translation> hostTranslations = hostsQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle updateSpeakerPoolJob = Entities.WithName("MoveSpeakers").WithReadOnly(hostsToSitSpeakers).WithReadOnly(hostTranslations).ForEach
        (
            (ref Translation translation, ref SpeakerComponent speaker, in SpeakerIndex index) =>
            {
                int attachedHosts = 0;
                float3 currentPos = translation.Value;
                float3 hostPosSum = new(0, 0, 0);
                
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
                    if (speaker._State == ConnectionState.Connected)
                    {
                        speaker._State = ConnectionState.Lingering;
                        speaker._AttachmentRadius = CalculateSpeakerRadius(attachParameters._ListenerPos, currentPos, attachParameters._LocalisationArcDegrees);
                    }
                    
                    if (speaker._State == ConnectionState.Lingering)
                    {
                        speaker._InactiveDuration += new TimeData().DeltaTime;
                        if (speaker._InactiveDuration >= attachParameters._SpeakerLingerTime)
                        {
                            speaker._State = ConnectionState.Disconnected;
                            speaker._AttachmentRadius = 0.001f;
                            currentPos = attachParameters._DisconnectedPosition;
                            speaker._InactiveDuration = 0;
                        }
                    }
                }
                else
                {
                    speaker._State = ConnectionState.Connected;
                    speaker._InactiveDuration = 0;
                    // Use lerped movement towards target if within attachment radius, otherwise snap to position.
                    float3 targetPos = hostPosSum / attachedHosts;
                    float3 lerpPos = math.lerp(currentPos, targetPos, attachParameters._TranslationSmoothing);
                    if (math.distance(currentPos, lerpPos) > speaker._AttachmentRadius)
                        currentPos = targetPos;
                    else
                        currentPos = lerpPos;

                    speaker._AttachmentRadius = CalculateSpeakerRadius(attachParameters._ListenerPos, currentPos, attachParameters._LocalisationArcDegrees);
                }
                translation.Value = currentPos;
                speaker._AttachedHostCount = attachedHosts;
            }
        ).WithDisposeOnCompletion(hostsToSitSpeakers)
        .WithDisposeOnCompletion(hostTranslations)
        .ScheduleParallel(updateHostRangeJob);
        updateSpeakerPoolJob.Complete();

        _CommandBufferSystem.AddJobHandleForProducer(updateSpeakerPoolJob);




        //----    CONNECT HOST TO IN-RANGE SPEAKER
        // TODO: check if better to remove speaker connection state - potentially relocate deactivated speakers on the main thread
        NativeArray<Entity> inRangeSpeaker = speakersQuery.ToEntityArray(Allocator.TempJob);
        JobHandle connectToActiveSpeakerJob = Entities.WithName("LinkToActiveSpeaker").
            WithNone<ConnectedTag>().WithAll<InListenerRadiusTag>().WithReadOnly(inRangeSpeaker).ForEach
        (
            (int entityInQueryIndex, Entity entity, ref HostComponent host, in Translation translation) =>
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
        ).WithDisposeOnCompletion(inRangeSpeaker)
        .ScheduleParallel(updateSpeakerPoolJob);
        connectToActiveSpeakerJob.Complete();

        _CommandBufferSystem.AddJobHandleForProducer(connectToActiveSpeakerJob);


        //----     SPAWN A POOLED SPEAKER ON A HOST IF NO NEARBY SPEAKERS WERE FOUND
        // TODO: Check if we can use "connected" host and speaker flags component to optimise search
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
                            pooledObj._AttachmentRadius = CalculateSpeakerRadius(attachParameters._ListenerPos, speakerTranslation.Value, attachParameters._LocalisationArcDegrees);
                            pooledObj._State = ConnectionState.Connected;
                            SetComponent(speakerEntities[s], pooledObj);

                            break;
                        }
                    }
                }
            }
        }).WithDisposeOnCompletion(hostEntities).WithDisposeOnCompletion(hosts).WithDisposeOnCompletion(hostTrans)
        .WithDisposeOnCompletion(speakerEntities).WithDisposeOnCompletion(speakers)
        .Schedule(connectToActiveSpeakerJob);
        speakerActivation.Complete();

        _CommandBufferSystem.AddJobHandleForProducer(speakerActivation);

        Dependency = speakerActivation;
    }

    public static float CalculateSpeakerRadius(float3 listenerPos, float3 speakerPos, float arcLength)
    {
        // Update attachment radius for new position. NOTE/TODO: Radius will be incorrect for next frame, need to investigate.
        float listenerCircumference = (float)(2 * Math.PI * math.distance(listenerPos, speakerPos));
        return arcLength / 360 * listenerCircumference;
    }
}


