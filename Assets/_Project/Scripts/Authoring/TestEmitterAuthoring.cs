using Unity.Entities;
using UnityEngine;

public class TestEmitterAuthoring : EmitterAuthoring
{
    public ContinuousProperties _Properties;

    public override void Initialise()
    {
        _EmitterType = EmitterType.Continuous;
    }

    public override void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        _EmitterEntity = entity;

        Debug.Log(name + "    is being converted to entity.");

        dstManager.AddComponentData(_EmitterEntity, new ContinuousComponent
        {
            _IsPlaying = !_ContactEmitter,
            _EmitterIndex = 100,
            _DistanceAmplitude = 1,
            _AudioClipIndex = 1,
            _PingPong = true,
            _SpeakerIndex = _SpeakerIndex,
            _LastSampleIndex = GrainSynth.Instance._CurrentDSPSample,
            _OutputSampleRate = AudioSettings.outputSampleRate,
        });

        _Initialised = true;
        Debug.Log(name + "    end of entity creation with initialised status:   " + _Initialised);

    }

    public override void UpdateComponents()
    {
        if (_IsPlaying && _InListenerRadius && _Connected && _Initialised)
        {
            Debug.Log("Emitter volume input source: " + _Properties._Volume._InputSource.name);
            Debug.Log("Emitter volume input value: " + _Properties._Volume._InputValue);
            Debug.Log("Emitter volume input get value: " + _Properties._Volume.GetValue());
            Debug.Log("");
        }
    }
}
