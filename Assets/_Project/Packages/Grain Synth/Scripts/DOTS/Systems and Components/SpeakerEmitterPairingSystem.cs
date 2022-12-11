using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Jobs;
using System;

// https://docs.unity3d.com/Packages/com.unity.entities@0.13/api/

/// <summary>
//     Processes dynamic emitter-speaker link components amd updates entity in-range statuses.
/// <summary>
[UpdateAfter(typeof(DOTS_QuadrantSystem))]
public class RangeCheckSystem : SystemBase
{
    protected override void OnUpdate()
    {
        SpeakerManagerComponent speakerManager = GetSingleton<SpeakerManagerComponent>();

        EntityQueryDesc speakerQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(GrainSpeakerComponent), typeof(PooledObjectComponent), typeof(Translation) }
        };

        //----    UPDATE ENTITY RANGE STATUSES
        EntityQuery speakerQuery = GetEntityQuery(speakerQueryDesc);
        NativeArray<PooledObjectComponent> pooledSpeakers = speakerQuery.ToComponentDataArray<PooledObjectComponent>(Allocator.TempJob);
        NativeArray<Translation> speakerTranslations = speakerQuery.ToComponentDataArray<Translation>(Allocator.TempJob);

        //----    EMITTERS
        JobHandle emitterRangeCheck = Entities.WithName("emitterRangeCheck").ForEach((ref ContinuousEmitterComponent emitter, in Translation trans) =>
        {
            if (!emitter._StaticallyLinked)
            {
                float emitterListenerDistance = math.distance(trans.Value, speakerManager._ListenerPos);
                bool inRangeCurrent = emitterListenerDistance < speakerManager._EmitterListenerMaxDistance;

                // Update activation status of emitters that have moved in/out of range from the listener.
                if (emitter._ListenerInRange && !inRangeCurrent)
                    emitter = UnLink(emitter);
                else if (!emitter._ListenerInRange && inRangeCurrent)
                    emitter._ListenerInRange = true;

                // Unlink emitters from their speaker if they have moved out of range from eachother
                if (emitter._LinkedToSpeaker)
                {
                    float emitterToSpeakerDist = math.distance(trans.Value, speakerTranslations[emitter._SpeakerIndex].Value);
                    if (pooledSpeakers[emitter._SpeakerIndex]._State == PooledObjectState.Pooled || emitterToSpeakerDist > speakerManager._EmitterSpeakerAttachRadius)
                    {
                        emitter = UnLink(emitter);
                    }
                }
            }
        }).WithDisposeOnCompletion(pooledSpeakers)
        .WithDisposeOnCompletion(speakerTranslations)
        .ScheduleParallel(this.Dependency);

        //----    SPEAKERS
        EntityQuery emitterLinkedQuery = GetEntityQuery(typeof(ContinuousEmitterComponent));
        NativeArray<ContinuousEmitterComponent> emitterLinks = GetEntityQuery(typeof(ContinuousEmitterComponent)).ToComponentDataArray<ContinuousEmitterComponent>(Allocator.TempJob);

        JobHandle speakerRangeCheck = Entities.WithName("speakerRangeCheck").ForEach((ref PooledObjectComponent poolObj, in GrainSpeakerComponent speaker, in Translation trans ) =>
        {
            float dist = math.distance(trans.Value, speakerManager._ListenerPos);
            bool inRangeCurrent = dist < speakerManager._EmitterListenerMaxDistance;
            // Pool out-of-range speakers
            if (poolObj._State == PooledObjectState.Active && !inRangeCurrent)
                poolObj._State = PooledObjectState.Pooled;

            // Pool speakers without linked emitters
            if (poolObj._State == PooledObjectState.Active)
            {
                int emitterIndex = -1;
                int attachedEmitters = 0;
                // Check if any emitters are attached to this speaker
                for (int e = 0; e < emitterLinks.Length; e++)
                {
                    if (emitterLinks[e]._SpeakerIndex == speaker._SpeakerIndex)
                    {
                        attachedEmitters++;
                        break;
                    }
                }
                // If no emitters are attatched, change speaker pooled status to "pooled"
                if (attachedEmitters == 0)
                    poolObj._State = PooledObjectState.Pooled;
                else if (attachedEmitters == 1)
                {
                }
            }
        }).WithDisposeOnCompletion(emitterLinks)
        .ScheduleParallel(this.Dependency);


        //----      FIND NEAREST SPEAKER FOR UNLINKED EMITTERS
        JobHandle rangeCheckDeps = JobHandle.CombineDependencies(emitterRangeCheck, speakerRangeCheck);
        NativeArray<Entity> speakerEnts = GetEntityQuery(typeof(GrainSpeakerComponent), typeof(Translation)).ToEntityArray(Allocator.TempJob);
        DSPTimerComponent dspTimer = GetSingleton<DSPTimerComponent>();       

        JobHandle activeSpeakersInRange = Entities.WithName("activeSpeakersInRange").ForEach((ref ContinuousEmitterComponent emitter, in Translation emitterTrans) =>
        {
            // Find emitters not currently linked to a speaker
            if (emitter._ListenerInRange && !emitter._LinkedToSpeaker)
            {
                float closestDist = float.MaxValue;
                int closestSpeakerIndex = int.MaxValue;

                // Find closest speaker to the emitter
                for (int i = 0; i < speakerEnts.Length; i++)
                {
                    if (GetComponent<PooledObjectComponent>(speakerEnts[i])._State == PooledObjectState.Active)
                    {
                        float dist = math.distance(emitterTrans.Value, GetComponent<Translation>(speakerEnts[i]).Value);
                        if (dist < speakerManager._EmitterSpeakerAttachRadius)
                        {
                            closestDist = dist;
                            closestSpeakerIndex = GetComponent<GrainSpeakerComponent>(speakerEnts[i])._SpeakerIndex;
                        }
                    }
                }
                // Attach the emitter to the nearest valid speaker
                if (closestSpeakerIndex != int.MaxValue)
                {
                    emitter._LinkedToSpeaker = true;
                    emitter._SpeakerIndex = closestSpeakerIndex;
                    emitter._LastGrainEmissionDSPIndex = dspTimer._CurrentDSPSample;
                }
            }
        }).WithDisposeOnCompletion(speakerEnts)
        .ScheduleParallel(rangeCheckDeps);




        //----     SPAWN A POOLED SPEAKER ON THE EMITTER IF NO NEARBY SPEAKERS WERE FOUND
        EntityQuery emitterQuery = GetEntityQuery(typeof(ContinuousEmitterComponent));
        NativeArray<Entity> emitterEnts = emitterQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<ContinuousEmitterComponent> emitters = GetEntityQuery(typeof(ContinuousEmitterComponent)).ToComponentDataArray<ContinuousEmitterComponent>(Allocator.TempJob);
        NativeArray<Entity> speakerEntities = speakerQuery.ToEntityArray(Allocator.TempJob);
        NativeArray<PooledObjectComponent> pooledSpeakerStates = speakerQuery.ToComponentDataArray<PooledObjectComponent>(Allocator.TempJob);        

        JobHandle speakerActivation = Job.WithName("speakerActivation").WithoutBurst().WithCode(() =>
        {
            bool spawned = false;
            // Find a pooled speaker
            for (int s = 0; s < pooledSpeakerStates.Length; s++)
            {
                if (pooledSpeakerStates[s]._State == PooledObjectState.Pooled && !spawned)
                {
                    // Find an unlinked emitter to link with pooled speaker
                    for (int e = 0; e < emitters.Length; e++)
                    {
                        if (emitters[e]._ListenerInRange && !emitters[e]._LinkedToSpeaker && !spawned)
                        {
                            spawned = true;

                            int speakerIndex = GetComponent<GrainSpeakerComponent>(speakerEntities[s])._SpeakerIndex;
                            float3 speakerPos = GetComponent<Translation>(emitterEnts[e]).Value;

                            // Update emitter component with speaker link
                            ContinuousEmitterComponent emitter = GetComponent<ContinuousEmitterComponent>(emitterEnts[e]);
                            emitter._LinkedToSpeaker = true;
                            emitter._SpeakerIndex = GetComponent<GrainSpeakerComponent>(speakerEntities[s])._SpeakerIndex;
                            emitter._LastGrainEmissionDSPIndex = dspTimer._CurrentDSPSample;
                            SetComponent(emitterEnts[e], emitter);

                            // Update speaker position
                            Translation speakerTrans = GetComponent<Translation>(speakerEntities[s]);
                            speakerTrans.Value = GetComponent<Translation>(emitterEnts[e]).Value;
                            SetComponent(speakerEntities[s], speakerTrans);

                            // Update speaker pooled status to active
                            PooledObjectComponent pooledObj = GetComponent<PooledObjectComponent>(speakerEntities[s]);
                            pooledObj._State = PooledObjectState.Active;
                            SetComponent(speakerEntities[s], pooledObj);

                            return;
                        }
                    }
                }
            }
        }).WithDisposeOnCompletion(speakerEntities).WithDisposeOnCompletion(pooledSpeakerStates)
        .WithDisposeOnCompletion(emitterEnts).WithDisposeOnCompletion(emitters)
        .Schedule(activeSpeakersInRange);

        this.Dependency = speakerActivation;
    }

    public static ContinuousEmitterComponent UnLink(ContinuousEmitterComponent emitter)
    { 
        emitter._LinkedToSpeaker = false;
        emitter._ListenerInRange = false;
        emitter._SpeakerIndex = int.MaxValue;

        return emitter;
    }
}