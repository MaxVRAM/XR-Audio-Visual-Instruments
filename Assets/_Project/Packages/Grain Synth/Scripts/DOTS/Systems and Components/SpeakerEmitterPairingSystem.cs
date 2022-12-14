using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using System;
using Substance.Game;

// https://docs.unity3d.com/Packages/com.unity.entities@0.13/api/

/// <summary>
//     Processes dynamic emitter-speaker link components amd updates entity in-range statuses.
/// <summary>
[UpdateAfter(typeof(DOTS_QuadrantSystem))]
public class RangeCheckSystem : SystemBase
{
    protected override void OnUpdate()
    {
        DSPTimerComponent dspTimer = GetSingleton<DSPTimerComponent>();       
        ActivationRadiusComponent activationRange = GetSingleton<ActivationRadiusComponent>();

        EntityQueryDesc speakerQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(SpeakerComponent), typeof(PoolingComponent), typeof(Translation) }
        };

        EntityQueryDesc emitterQueryDesc = new EntityQueryDesc
        {
            None = new ComponentType[] { typeof(FixedSpeakerLinkTag) },
            All = new ComponentType[] { typeof(ContinuousEmitterComponent), typeof(Translation) }
        };

        EntityQuery speakersQuery = GetEntityQuery(speakerQueryDesc);
        EntityQuery emittersQuery = GetEntityQuery(emitterQueryDesc);


        //----    UPDATE ENTITY IN-RANGE STATUSES
        NativeArray<PoolingComponent> speakerPool = speakersQuery.ToComponentDataArray<PoolingComponent>(Allocator.TempJob);
        NativeArray<Translation> speakerTranslations = speakersQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle checkEmitterRangeJob = Entities.WithName("CheckEmitterRange").ForEach
        (
            (ref ContinuousEmitterComponent emitter, in Translation trans) =>
            {
                if (!emitter._FixedSpeakerLink)
                {
                    // Calculate if emitter is currently in-range
                    float emitterListenerDistance = math.distance(trans.Value, activationRange._ListenerPos);
                    bool inListenerRadiusNow = emitterListenerDistance < activationRange._EmitterToListenerRadius;

                    // Update in-range status of emitters from the listener.
                    if (!inListenerRadiusNow)
                    {
                        emitter._InListenerRadius = false;
                        emitter._SpeakerAttached = false;
                        emitter._SpeakerIndex = int.MaxValue;
                    }
                    else emitter._InListenerRadius = true;

                    // Unlink emitters outside speaker attachment radius.
                    if (emitter._SpeakerAttached)
                    {
                        float emitterToSpeakerDist = math.distance(trans.Value, speakerTranslations[emitter._SpeakerIndex].Value);
                        if (speakerPool[emitter._SpeakerIndex]._State == PooledState.Pooled || emitterToSpeakerDist > activationRange._SpeakerAttachRadius)
                        {
                            emitter._SpeakerAttached = false;
                            emitter._SpeakerIndex = int.MaxValue;
                        }
                    }
                }
            }
        ).WithDisposeOnCompletion(speakerPool)
        .WithDisposeOnCompletion(speakerTranslations)
        .ScheduleParallel(Dependency);




        //----    DEACTIVATE SPEAKERS WITHOUT ATTACHED ENTITIES
        NativeArray<ContinuousEmitterComponent> emitterAttached = emittersQuery.ToComponentDataArray<ContinuousEmitterComponent>(Allocator.TempJob);
        JobHandle updateSpeakerPoolJob = Entities.WithName("UpdateSpeakerPool").ForEach
        (
            (ref PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                if (pooling._State == PooledState.Active)
                {
                    for (int e = 0; e < emitterAttached.Length; e++)
                        if (emitterAttached[e]._SpeakerIndex == speaker._SpeakerIndex)
                            // Exit job iteration before ending loop / pooling speaker
                            return;
                    pooling._State = PooledState.Pooled;
                }
            }
        ).WithDisposeOnCompletion(emitterAttached)
        .ScheduleParallel(checkEmitterRangeJob);




        //----    ATTACH EMITTER TO ACTIVE IN-RANGE SPEAKER
        NativeArray<Entity> inRangeSpeaker = speakersQuery.ToEntityArray(Allocator.TempJob);
        JobHandle linkToActiveSpeakerJob = Entities.WithName("LinkToActiveSpeaker").ForEach
        (
            (ref ContinuousEmitterComponent emitter, in Translation emitterTranslation) =>
            {
                // Find emitters in listener radius not currently linked to a speaker
                if (!emitter._SpeakerAttached && emitter._InListenerRadius)
                {
                    float closestDist = float.MaxValue;
                    int closestSpeakerIndex = int.MaxValue;
                    // Find closest speaker to the emitter
                    for (int i = 0; i < inRangeSpeaker.Length; i++)
                    {
                        if (GetComponent<PoolingComponent>(inRangeSpeaker[i])._State == PooledState.Active)
                        {
                            float dist = math.distance(emitterTranslation.Value, GetComponent<Translation>(inRangeSpeaker[i]).Value);
                            if (dist < activationRange._SpeakerAttachRadius)
                            {
                                closestDist = dist;
                                closestSpeakerIndex = GetComponent<SpeakerComponent>(inRangeSpeaker[i])._SpeakerIndex;
                            }
                        }
                    }
                    // Attach the emitter to the nearest valid speaker
                    if (closestSpeakerIndex != int.MaxValue)
                    {
                        emitter._SpeakerAttached = true;
                        emitter._SpeakerIndex = closestSpeakerIndex;
                        emitter._LastSampleIndex = dspTimer._CurrentSampleIndex;
                    }
                }
            }
        ).WithDisposeOnCompletion(inRangeSpeaker)
        .ScheduleParallel(updateSpeakerPoolJob);




        //----     SPAWN A POOLED SPEAKER ON THE EMITTER IF NO NEARBY SPEAKERS WERE FOUND
        NativeArray<Entity> emitterEntities = emittersQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<ContinuousEmitterComponent> emitters = emittersQuery.ToComponentDataArray<ContinuousEmitterComponent>(Allocator.TempJob);
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
                    // Find an unlinked emitter to link with pooled speaker
                    for (int e = 0; e < emitters.Length; e++)
                    {
                        if (emitters[e]._InListenerRadius && !emitters[e]._SpeakerAttached)
                        {
                            spawned = true;
                            float3 speakerPos = GetComponent<Translation>(emitterEntities[e]).Value;

                            // Update emitter component with speaker link
                            ContinuousEmitterComponent emitter = GetComponent<ContinuousEmitterComponent>(emitterEntities[e]);
                            emitter._SpeakerAttached = true;
                            emitter._SpeakerIndex = GetComponent<SpeakerComponent>(speakerEntities[s])._SpeakerIndex;
                            emitter._LastSampleIndex = dspTimer._CurrentSampleIndex;
                            SetComponent(emitterEntities[e], emitter);

                            // Update speaker position
                            Translation speakerTrans = GetComponent<Translation>(speakerEntities[s]);
                            speakerTrans.Value = GetComponent<Translation>(emitterEntities[e]).Value;
                            SetComponent(speakerEntities[s], speakerTrans);

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
        .WithDisposeOnCompletion(emitterEntities).WithDisposeOnCompletion(emitters)
        .Schedule(linkToActiveSpeakerJob);




        NativeArray<ContinuousEmitterComponent> emitterToSitOn = emittersQuery.ToComponentDataArray<ContinuousEmitterComponent>(Allocator.TempJob);
        NativeArray<Translation> emitterTranslations = emittersQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        JobHandle moveSpeakersJob = Entities.WithName("MoveSpeakers").ForEach
        (
            (ref Translation translation, in PoolingComponent pooling, in SpeakerComponent speaker) =>
            {
                // TODO ---- Triangulate attached emitter positions and move speaker to the centre
                if (pooling._State == PooledState.Active)
                {
                    int emitterIndex = -1;
                    int attachedEmitters = 0;
                    // Check if any emitters are attached to this speaker
                    for (int e = 0; e < emitterToSitOn.Length; e++)
                        if (emitterToSitOn[e]._SpeakerIndex == speaker._SpeakerIndex)
                        {
                            emitterIndex = e;
                            attachedEmitters++;
                        }
                    if (attachedEmitters == 1 && emitterIndex != -1)
                        translation = emitterTranslations[emitterIndex];
                }
            }
        ).WithDisposeOnCompletion(emitterToSitOn)
        .WithDisposeOnCompletion(emitterTranslations)
        .ScheduleParallel(speakerActivation);

        Dependency = moveSpeakersJob;
    }
}