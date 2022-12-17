using Unity.Burst;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using System;
using System.Linq;
using Unity.Entities.UniversalDelegates;
using Unity.Entities.CodeGeneratedJobForEach;
using Random = UnityEngine.Random;
using System.ComponentModel;
using Unity.Jobs.LowLevel.Unsafe;

/// <summary>
//     Populate random value buffer with unique values for each thread.
/// <summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
class RandomSystem : ComponentSystem
{
    public NativeArray<Unity.Mathematics.Random> RandomArray { get; private set; }
    protected override void OnCreate()
    {
        var randomArray = new Unity.Mathematics.Random[JobsUtility.MaxJobThreadCount];
        var seed = new System.Random();

        for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            randomArray[i] = new Unity.Mathematics.Random((uint)seed.Next());

        RandomArray = new NativeArray<Unity.Mathematics.Random>(randomArray, Allocator.Persistent);
    }
    protected override void OnDestroy()
        => RandomArray.Dispose();

    protected override void OnUpdate() { }
}

[UpdateAfter(typeof(AttachmentSystem))]
public class GrainSynthSystem : SystemBase
{
    // Command buffer for removing tween components once they are completed
    private EndSimulationEntityCommandBufferSystem _CommandBufferSystem;

    protected override void OnCreate()
    {
        base.OnCreate();
        _CommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        int sampleRate = AudioSettings.outputSampleRate;

        // Acquire an ECB and convert it to a concurrent one to be able to use it from a parallel job.
        EntityCommandBuffer.ParallelWriter entityCommandBuffer = _CommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
        
        // ----------------------------------- EMITTER UPDATE
        // Get all audio clip data components
        NativeArray<AudioClipDataComponent> audioClipData = GetEntityQuery(typeof(AudioClipDataComponent)).ToComponentDataArray<AudioClipDataComponent>(Allocator.TempJob);
        WindowingDataComponent windowingData = GetSingleton<WindowingDataComponent>();
        DSPTimerComponent dspTimer = GetSingleton<DSPTimerComponent>();
        float dt = Time.DeltaTime;
        var randomArray = World.GetExistingSystem<RandomSystem>().RandomArray;

        #region EMIT GRAINS
        //---   CREATES ENTITIES W/ GRAIN PROCESSOR + GRAIN SAMPLE BUFFER + DSP SAMPLE BUFFER + DSP PARAMS BUFFER

        JobHandle emitGrains = Entities.WithNativeDisableParallelForRestriction(randomArray).ForEach
        (
            (int nativeThreadIndex, int entityInQueryIndex, ref DynamicBuffer<DSPParametersElement> dspChain, ref ContinuousComponent emitter, ref InListenerRadiusTag listenerRadius, ref PlayingTag playing) =>
            {
                if (emitter._Connected)
                {
                    // Max grains to stop it getting stuck in a while loop
                    int maxGrains = 50;
                    int grainCount = 0;

                    int dspTailLength = 0;

                    // Get new random values
                    var randomGen = randomArray[nativeThreadIndex];
                    float randomDuration = randomGen.NextFloat(-1, 1);
                    float randomDensity = randomGen.NextFloat(-1, 1);
                    float randomPlayhead = randomGen.NextFloat(-1, 1);
                    float randomVolume = randomGen.NextFloat(-1, 1);
                    float randomTranspose = randomGen.NextFloat(-1, 1);

                    // Compute first grain value
                    int duration = (int)ComputeEmitterParameter(emitter._Duration, randomDuration);
                    float density = ComputeEmitterParameter(emitter._Density, randomDensity);
                    int offset = (int)(emitter._PreviousGrainDuration / density);
                    int sampleIndexNextGrainStart = emitter._LastSampleIndex + offset;
                    float playhead = ComputeEmitterParameter(emitter._Playhead, randomPlayhead);
                    float volume = ComputeEmitterParameter(emitter._Volume, randomVolume);
                    float transpose = ComputeEmitterParameter(emitter._Transpose, randomTranspose);
                    float pitch = Mathf.Pow(2, Mathf.Clamp(transpose, -4f, 4f));

                    // Create new grain
                    while (sampleIndexNextGrainStart <= dspTimer._CurrentSampleIndex + dspTimer._GrainQueueDuration && grainCount < maxGrains)
                    {
                        if (volume * emitter._DistanceAmplitude > 0.01f)
                        {
                            // Increase grain count to avoid infinite loop
                            grainCount++;

                            // Find the largest delay DSP effect tail in the chain so that the tail can be added to the sample and DSP buffers
                            for (int j = 0; j < dspChain.Length; j++)
                                if (dspChain[j]._DelayBasedEffect)
                                    if (dspChain[j]._SampleTail > dspTailLength)
                                        dspTailLength = dspChain[j]._SampleTail;

                            dspTailLength = Mathf.Clamp(dspTailLength, 0, emitter._OutputSampleRate - duration);


                            Entity grainProcessorEntity = entityCommandBuffer.CreateEntity(entityInQueryIndex);

                            //Add ping-pong tag if needed
                            int clipLength = audioClipData[emitter._AudioClipIndex]._ClipDataBlobAsset.Value.array.Length;
                            if (emitter._PingPong)
                            {
                                if (playhead * clipLength + duration * pitch >= clipLength)
                                {
                                    entityCommandBuffer.AddComponent(entityInQueryIndex, grainProcessorEntity, new PingPongTag());
                                }
                            }

                            // Build grain processor entity
                            entityCommandBuffer.AddComponent(entityInQueryIndex, grainProcessorEntity, new GrainProcessorComponent
                            {
                                _AudioClipDataComponent = audioClipData[emitter._AudioClipIndex],

                                _PlayheadNorm = playhead,
                                _SampleCount = duration,

                                _Pitch = pitch,
                                _Volume = volume * emitter._DistanceAmplitude,

                                _SpeakerIndex = emitter._SpeakerIndex,
                                _StartSampleIndex = sampleIndexNextGrainStart,
                                _SamplePopulated = false,

                                _EffectTailSampleLength = dspTailLength
                            });

                            // Attach sample and DSP buffers to grain processor
                            entityCommandBuffer.AddBuffer<GrainSampleBufferElement>(entityInQueryIndex, grainProcessorEntity);
                            entityCommandBuffer.AddBuffer<DSPSampleBufferElement>(entityInQueryIndex, grainProcessorEntity);

                            // Add DSP parameters to grain processor
                            DynamicBuffer<DSPParametersElement> dspParameters = entityCommandBuffer.AddBuffer<DSPParametersElement>(entityInQueryIndex, grainProcessorEntity);

                            for (int i = 0; i < dspChain.Length; i++)
                            {
                                DSPParametersElement tempParams = dspChain[i];
                                tempParams._SampleStartTime = sampleIndexNextGrainStart;
                                dspParameters.Add(tempParams);
                            }
                        }

                        // Remember this grain's timing values for next iteration
                        emitter._LastSampleIndex = sampleIndexNextGrainStart;
                        emitter._PreviousGrainDuration = duration;

                        // Get random values for next iteration and update random array to avoid repeating values
                        randomPlayhead = randomGen.NextFloat(-1, 1);
                        randomVolume = randomGen.NextFloat(-1, 1);
                        randomTranspose = randomGen.NextFloat(-1, 1);
                        randomDuration = randomGen.NextFloat(-1, 1);
                        randomDensity = randomGen.NextFloat(-1, 1);
                        randomArray[nativeThreadIndex] = randomGen;

                        // Compute grain values for next iteration
                        duration = (int)ComputeEmitterParameter(emitter._Duration, randomDuration);
                        density = ComputeEmitterParameter(emitter._Density, randomDensity);
                        offset = (int)(duration / density);
                        sampleIndexNextGrainStart += offset;
                        playhead = ComputeEmitterParameter(emitter._Playhead, randomPlayhead);
                        volume = ComputeEmitterParameter(emitter._Volume, randomVolume);
                        transpose = ComputeEmitterParameter(emitter._Transpose, randomTranspose);
                        pitch = Mathf.Pow(2, Mathf.Clamp(transpose, -4f, 4f));
                    }
                }
            }
        ).ScheduleParallel(this.Dependency);

        // Make sure that the ECB system knows about our job
        _CommandBufferSystem.AddJobHandleForProducer(emitGrains);
        #endregion

        #region BURST GRAINS
        JobHandle emitBurst = Entities.WithNativeDisableParallelForRestriction(randomArray).ForEach
        (
            (int nativeThreadIndex, int entityInQueryIndex, ref DynamicBuffer<DSPParametersElement> dspChain, ref BurstComponent burst, ref InListenerRadiusTag listenerRadius, ref PlayingTag playing) =>
            {
                if (burst._Connected)
                {
                    int grainsCreated = 0;

                    // TODO - CHECK IF THIS NEEDS TO HAVE GRAIN QUEUE DURATION ADDED, POSSIBLE CAUSE OF UNNECESSARY LATENCY ON BURST TRIGGER
                    int currentDSPTime = dspTimer._CurrentSampleIndex + dspTimer._GrainQueueDuration;
                    int dspTailLength = 0;
                    var randomGen = randomArray[nativeThreadIndex];

                    int burstDurationRange = (int)(burst._BurstDuration._Max - burst._BurstDuration._Min);
                    int burstDurationInteraction = (int)(Map(burst._BurstDuration._InteractionInput, 0, 1, 0, 1, burst._BurstDuration._Shape) * burst._BurstDuration._InteractionAmount);
                    int burstDurationRandom = (int)(randomGen.NextFloat(-1, 1) * burst._BurstDuration._Noise * burstDurationRange);

                    int totalBurstSampleCount = (int)(Mathf.Clamp(burst._BurstDuration._StartValue + burstDurationInteraction + burstDurationRandom,
                        burst._BurstDuration._Min, burst._BurstDuration._Max));

                    float randomDensity = randomGen.NextFloat(-1, 1);
                    float randomPlayhead = randomGen.NextFloat(-1, 1);
                    float randomGrainDuration = randomGen.NextFloat(-1, 1);
                    float randomVolume = randomGen.NextFloat(-1, 1);
                    float randomTranspose = randomGen.NextFloat(-1, 1);

                    // Compute first grain value
                    int offset = 0;
                    float playhead = ComputeBurstParameter(burst._Playhead, offset, totalBurstSampleCount, randomPlayhead);
                    int duration = (int)ComputeBurstParameter(burst._GrainDuration, offset, totalBurstSampleCount, randomGrainDuration);
                    float density = ComputeBurstParameter(burst._Density, offset, totalBurstSampleCount, randomDensity);
                    float transpose = ComputeBurstParameter(burst._Transpose, offset, totalBurstSampleCount, randomTranspose);
                    float volume = ComputeBurstParameter(burst._Volume, offset, totalBurstSampleCount, randomVolume);
                    float pitch = Mathf.Pow(2, Mathf.Clamp(transpose, -4f, 4f));

                    while (offset < totalBurstSampleCount)
                    {
                        if (volume * burst._DistanceAmplitude > 0.01f)
                        {
                            grainsCreated++;

                            // Find the largest delay DSP effect tail in the chain so that the tail can be added to the sample and DSP buffers
                            for (int j = 0; j < dspChain.Length; j++)
                                if (dspChain[j]._DelayBasedEffect)
                                    if (dspChain[j]._SampleTail > dspTailLength)
                                        dspTailLength = dspChain[j]._SampleTail;

                            dspTailLength = Mathf.Clamp(dspTailLength, 0, burst._OutputSampleRate - duration);

                            // Build grain processor entity
                            Entity grainProcessorEntity = entityCommandBuffer.CreateEntity(entityInQueryIndex);

                            //Add ping-pong tag if needed
                            int clipLength = audioClipData[burst._AudioClipIndex]._ClipDataBlobAsset.Value.array.Length;
                            if (burst._PingPong)
                            {
                                if (playhead * clipLength + duration * pitch >= clipLength)
                                {
                                    entityCommandBuffer.AddComponent(entityInQueryIndex, grainProcessorEntity, new PingPongTag());
                                }
                            }

                            // Then add grain data
                            entityCommandBuffer.AddComponent(entityInQueryIndex, grainProcessorEntity, new GrainProcessorComponent
                            {
                                _AudioClipDataComponent = audioClipData[burst._AudioClipIndex],

                                _PlayheadNorm = playhead,
                                _SampleCount = duration,

                                _Pitch = pitch,
                                _Volume = volume * burst._DistanceAmplitude,

                                _SpeakerIndex = burst._SpeakerIndex,
                                _StartSampleIndex = offset + currentDSPTime,
                                _SamplePopulated = false,

                                _EffectTailSampleLength = dspTailLength
                            });

                            // Attach sample and DSP buffers to grain processor
                            entityCommandBuffer.AddBuffer<GrainSampleBufferElement>(entityInQueryIndex, grainProcessorEntity);
                            entityCommandBuffer.AddBuffer<DSPSampleBufferElement>(entityInQueryIndex, grainProcessorEntity);

                            // Add DSP parameters to grain processor
                            DynamicBuffer<DSPParametersElement> dspParameters = entityCommandBuffer.AddBuffer<DSPParametersElement>(entityInQueryIndex, grainProcessorEntity);

                            for (int i = 0; i < dspChain.Length; i++)
                            {
                                DSPParametersElement tempParams = dspChain[i];
                                tempParams._SampleStartTime = offset + currentDSPTime;
                                dspParameters.Add(tempParams);
                            }
                        }

                        // Get random values for next iteration and update random array to avoid repeating values
                        if (!burst._Density._LockNoise)
                            randomDensity = randomGen.NextFloat(-1, 1);
                        if (!burst._Playhead._LockNoise)
                            randomPlayhead = randomGen.NextFloat(-1, 1);
                        if (!burst._GrainDuration._LockNoise)
                            randomGrainDuration = randomGen.NextFloat(-1, 1);
                        randomVolume = randomGen.NextFloat(-1, 1);
                        if (!burst._Transpose._LockNoise)
                            randomTranspose = randomGen.NextFloat(-1, 1);

                        randomArray[nativeThreadIndex] = randomGen;

                        // Compute grain values for next iteration
                        offset += (int)(duration / density);
                        density = ComputeBurstParameter(burst._Density, offset, totalBurstSampleCount, randomDensity);
                        playhead = ComputeBurstParameter(burst._Playhead, offset, totalBurstSampleCount, randomPlayhead);
                        duration = (int)ComputeBurstParameter(burst._GrainDuration, offset, totalBurstSampleCount, randomGrainDuration);
                        transpose = ComputeBurstParameter(burst._Transpose, offset, totalBurstSampleCount, randomTranspose);
                        volume = ComputeBurstParameter(burst._Volume, offset, totalBurstSampleCount, randomVolume);
                        pitch = Mathf.Pow(2, Mathf.Clamp(transpose, -4f, 4f));
                        }

                    burst._IsPlaying = false;
                }
            }
        ).WithDisposeOnCompletion(audioClipData)
        .ScheduleParallel(emitGrains);

        // Make sure that the ECB system knows about our job
        _CommandBufferSystem.AddJobHandleForProducer(emitBurst);

        #endregion

        #region POPULATE GRAINS
        // ----------------------------------- GRAIN PROCESSOR UPDATE
        //---   TAKES GRAIN PROCESSOR INFORMATION AND FILLS THE SAMPLE BUFFER + DSP BUFFER (W/ 0s TO THE SAME LENGTH AS SAMPLE BUFFER)
        JobHandle processGrains = Entities.WithNone<PingPongTag>().ForEach
        (
            (int entityInQueryIndex, DynamicBuffer <GrainSampleBufferElement> sampleOutputBuffer, DynamicBuffer<DSPSampleBufferElement> dspBuffer, ref GrainProcessorComponent grain) =>
            {
                if (!grain._SamplePopulated)
                {
                    ref BlobArray<float> clipArray = ref grain._AudioClipDataComponent._ClipDataBlobAsset.Value.array;
                    float sourceIndex = grain._PlayheadNorm * clipArray.Length;
                    float increment = grain._Pitch;

                    // Suck up all the juicy samples from the source content
                    for (int i = 0; i < grain._SampleCount - 1; i++)
                    {
                        // Set rate of sample read to alter pitch - interpolate sample if not integer to create 
                        sourceIndex += increment;
                        float sourceIndexRemainder = sourceIndex % 1;
                        float sourceValue;

                        if (sourceIndex + 1 >= clipArray.Length)
                            sampleOutputBuffer.Add(new GrainSampleBufferElement { Value = 0 });
                        else
                        {
                            if (sourceIndexRemainder != 0)
                                sourceValue = math.lerp(clipArray[(int)sourceIndex], clipArray[(int)sourceIndex + 1], sourceIndexRemainder);
                            else
                                sourceValue = grain._AudioClipDataComponent._ClipDataBlobAsset.Value.array[(int)sourceIndex];

                            // Adjusted for volume and windowing
                            sourceValue *= grain._Volume;
                            sourceValue *= windowingData._WindowingArray.Value.array[(int)Map(i, 0, grain._SampleCount, 0, windowingData._WindowingArray.Value.array.Length)];

                            sampleOutputBuffer.Add(new GrainSampleBufferElement { Value = sourceValue });
                        }

                        dspBuffer.Add(new DSPSampleBufferElement { Value = 0 });
                    }

                    // --Add additional samples to increase grain playback size based on DSP effect tail length
                    for (int i = 0; i < grain._EffectTailSampleLength; i++)
                    {
                        sampleOutputBuffer.Add(new GrainSampleBufferElement { Value = 0 });
                        dspBuffer.Add(new DSPSampleBufferElement { Value = 0 });
                    }
                    grain._SamplePopulated = true; // TODO - SWAP THIS TO A TAG COMPONENT TO STOP HAVING TO USE GRAINM PROCESSOR AS A REF INPUT AND AVOID IF STATEMENTS IN THIS AND DSP JOB
                }
            }
        ).ScheduleParallel(emitBurst);
        #endregion

        #region POPULATE PiNG PONG GRAINS
        //-----------------------------------GRAIN PROCESSOR UPDATE
        //---TAKES GRAIN PROCESSOR INFORMATION AND FILLS THE SAMPLE BUFFER +DSP BUFFER(W / 0s TO THE SAME LENGTH AS SAMPLE BUFFER)
        JobHandle processPingPongGrains = Entities.ForEach
        (
            (int entityInQueryIndex, DynamicBuffer <GrainSampleBufferElement> sampleOutputBuffer, DynamicBuffer<DSPSampleBufferElement> dspBuffer, ref GrainProcessorComponent grain, in PingPongTag pingpong) =>
            {
                if (!grain._SamplePopulated)
                {
                    int clipLength = grain._AudioClipDataComponent._ClipDataBlobAsset.Value.array.Length;
                    float sourceIndex = grain._PlayheadNorm * clipLength;
                    float increment = grain._Pitch;

                    for (int i = 0; i < grain._SampleCount; i++)
                    {
                        // Ping pong samples at the limit of the sample
                        if (sourceIndex + increment < 0 || sourceIndex + increment > clipLength - 1)
                        {
                            // Invert the source sample increment
                            increment = -increment;
                        }
                        // Set rate of sample read to alter pitch - interpolate sample if not integer to create 
                        sourceIndex += increment;
                        float sourceIndexRemainder = sourceIndex % 1;
                        float sourceValue;
                        if (sourceIndexRemainder != 0)
                        {
                            sourceValue = math.lerp(
                                grain._AudioClipDataComponent._ClipDataBlobAsset.Value.array[(int)sourceIndex],
                                grain._AudioClipDataComponent._ClipDataBlobAsset.Value.array[(int)sourceIndex + 1],
                                sourceIndexRemainder);
                        }
                        else sourceValue = grain._AudioClipDataComponent._ClipDataBlobAsset.Value.array[(int)sourceIndex];

                        // Adjusted for volume and windowing
                        sourceValue *= grain._Volume;
                        sourceValue *= windowingData._WindowingArray.Value.array[
                            (int)Map(i, 0, grain._SampleCount, 0, windowingData._WindowingArray.Value.array.Length)];
                        sampleOutputBuffer.Add(new GrainSampleBufferElement { Value = sourceValue });
                        dspBuffer.Add(new DSPSampleBufferElement { Value = 0 });
                    }

                    // --Add additional samples to increase grain playback size based on DSP effect tail length
                    for (int i = 0; i < grain._EffectTailSampleLength; i++)
                    {
                        sampleOutputBuffer.Add(new GrainSampleBufferElement { Value = 0 });
                        dspBuffer.Add(new DSPSampleBufferElement { Value = 0 });
                    }

                    grain._SamplePopulated = true; // TODO - SWAP THIS TO A TAG COMPONENT TO STOP HAVING TO USE GRAINM PROCESSOR AS A REF INPUT AND AVOID IF STATEMENTS IN THIS AND DSP JOB
                }
            }
        ).ScheduleParallel(processGrains);
        #endregion

        #region DSP CHAIN        
        JobHandle dspGrains = Entities.ForEach
        (
           (DynamicBuffer<DSPParametersElement> dspParamsBuffer, DynamicBuffer<GrainSampleBufferElement> sampleOutputBuffer, DynamicBuffer < DSPSampleBufferElement > dspBuffer, ref GrainProcessorComponent grain) =>
           {
               if (grain._SamplePopulated)
               {
                   for (int i = 0; i < dspParamsBuffer.Length; i++)
                   {
                       switch (dspParamsBuffer[i]._DSPType)
                       {
                           case DSPTypes.Bitcrush:
                               DSP_Bitcrush.ProcessDSP(dspParamsBuffer[i], sampleOutputBuffer, dspBuffer);
                               break;
                           case DSPTypes.Delay:
                               break;
                           case DSPTypes.Flange:
                               DSP_Flange.ProcessDSP(dspParamsBuffer[i], sampleOutputBuffer, dspBuffer);
                               break;
                           case DSPTypes.Filter:
                               DSP_Filter.ProcessDSP(dspParamsBuffer[i], sampleOutputBuffer, dspBuffer);
                               break;
                           case DSPTypes.Chopper:
                               DSP_Chopper.ProcessDSP(dspParamsBuffer[i], sampleOutputBuffer, dspBuffer);
                               break;
                       }
                   }
               }
           }
        ).ScheduleParallel(processPingPongGrains);
        #endregion

        Dependency = dspGrains;
    }

    #region HELPERS
    public static float Map(float val, float inMin, float inMax, float outMin, float outMax)
    {
        return outMin + ((outMax - outMin) / (inMax - inMin)) * (val - inMin);
    }
    public static float Map(float val, float inMin, float inMax, float outMin, float outMax, float exp)
    {
        return Mathf.Pow((val - inMin) / (inMax - inMin), exp) * (outMax - outMin) + outMin;
    }
    public static float ComputeEmitterParameter(ModulationComponent mod, float r)
    {
        float interaction = Mathf.Pow(mod._InteractionInput / 1, mod._Shape) * mod._InteractionAmount;
        float random;
        if (mod._PerlinNoise)
            random = mod._PerlinValue * mod._Noise * Mathf.Abs(mod._Max - mod._Min);
        else
            random = r * mod._Noise * Mathf.Abs(mod._Max - mod._Min);
        return Mathf.Clamp(mod._StartValue + interaction + random, mod._Min, mod._Max);
    }
    public static float ComputeBurstParameter(ModulationComponent mod, float t, float n, float r)
    {
        float timeShaped = Mathf.Pow(t / n, mod._Shape);
        float modulationOverTime = timeShaped * (mod._EndValue - mod._StartValue);
        float interaction = mod._InteractionAmount * mod._InteractionInput;
        if (mod._LockStartValue)
            interaction *= timeShaped;
        else if (mod._LockEndValue)
            interaction *= 1 - timeShaped;
        float random = r * mod._Noise * Mathf.Abs(mod._Max - mod._Min);
        return Mathf.Clamp(mod._StartValue + modulationOverTime + interaction + random, mod._Min, mod._Max);
    }

    #endregion
}