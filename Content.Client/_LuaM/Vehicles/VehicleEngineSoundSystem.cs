/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._LuaM.Vehicles.Components;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Vehicles;

// -- Гул мотора собирается из четырёх зацикленных слоёв, которые плавно перетекают друг в друга по скорости
public sealed partial class VehicleEngineSoundSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly float[] Centers = { 0f, 0.3f, 0.62f, 0.95f };
    private const float LayerWidth = 0.34f;
    private const float RpmSmoothing = 4f;
    private const float MinSkidSpeed = 1f;

    private readonly Dictionary<EntityUid, EngineAudio> _engines = new();
    private readonly List<EntityUid> _stale = new();

    private sealed class EngineAudio
    {
        public readonly EntityUid?[] Layers = new EntityUid?[4];
        public EntityUid? Skid;
        public float Rpm;
        public float OffTime;
        public bool Seen;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleEngineComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<VehicleEngineComponent> ent, ref ComponentShutdown args)
    {
        if (_engines.Remove(ent, out var audio))
            StopAll(audio);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var listener = _audio.GetListenerCoordinates();
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<VehicleEngineComponent, GridVehicleMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var engine, out var mover, out var xform))
        {
            var pos = _transform.GetMapCoordinates(uid, xform);
            var inRange = pos.MapId == listener.MapId &&
                          (pos.Position - listener.Position).LengthSquared() <= engine.MaxDistance * engine.MaxDistance;
            var running = engine.EngineOn && now >= engine.ReadyAt;

            if (!inRange)
            {
                if (_engines.Remove(uid, out var old))
                    StopAll(old);

                continue;
            }

            _engines.TryGetValue(uid, out var audio);
            var gain = 1f;
            if (running)
            {
                if (audio == null)
                {
                    audio = new EngineAudio();
                    _engines[uid] = audio;
                }

                audio.OffTime = 0f;
            }
            else
            {
                // -- Заглушенный мотор ещё немного гудит под звук глушения и плавно затихает
                if (audio == null)
                    continue;

                audio.OffTime += frameTime;
                gain = 1f - Math.Clamp((audio.OffTime - engine.StopFadeDelay) / MathF.Max(engine.StopFadeTime, 0.01f), 0f, 1f);
                if (gain <= 0f)
                {
                    _engines.Remove(uid);
                    StopAll(audio);
                    continue;
                }
            }

            audio.Seen = true;
            var speed = MathF.Abs(mover.CurrentSpeed);
            var target = mover.MaxSpeed > 0f ? Math.Clamp(speed / mover.MaxSpeed, 0f, 1f) : 0f;
            audio.Rpm += (target - audio.Rpm) * Math.Clamp(frameTime * RpmSmoothing, 0f, 1f);

            UpdateLayer(uid, engine, audio, 0, engine.IdleSound, gain);
            UpdateLayer(uid, engine, audio, 1, engine.SlowSound, gain);
            UpdateLayer(uid, engine, audio, 2, engine.MediumSound, gain);
            UpdateLayer(uid, engine, audio, 3, engine.FastSound, gain);

            var skidding = running && speed >= MinSkidSpeed && (mover.Handbrake || MathF.Abs(mover.DriftDegrees) >= engine.SkidDegrees);
            if (skidding && audio.Skid == null)
            {
                audio.Skid = Play(uid, engine.SkidSound, engine.SkidVolume, engine.MaxDistance);
                _audio.PlayEntity(engine.SkidStartSound, Filter.Local(), uid, false, AudioParams.Default.WithVolume(engine.SkidVolume).WithMaxDistance(engine.MaxDistance));
            }
            else if (!skidding && audio.Skid != null)
            {
                audio.Skid = _audio.Stop(audio.Skid);
            }
        }

        _stale.Clear();
        foreach (var (uid, audio) in _engines)
        {
            if (!audio.Seen)
                _stale.Add(uid);

            audio.Seen = false;
        }

        foreach (var uid in _stale)
        {
            if (_engines.Remove(uid, out var audio))
                StopAll(audio);
        }
    }

    private void UpdateLayer(EntityUid uid, VehicleEngineComponent engine, EngineAudio audio, int index, SoundSpecifier? sound, float gain)
    {
        if (sound == null)
            return;

        var weight = MathF.Max(0f, 1f - MathF.Abs(audio.Rpm - Centers[index]) / LayerWidth);
        if (index == 0 && audio.Rpm <= Centers[0])
            weight = 1f;

        weight *= gain;

        audio.Layers[index] ??= Play(uid, sound, float.NegativeInfinity, engine.MaxDistance);
        if (audio.Layers[index] is not { } stream || !TryComp<AudioComponent>(stream, out var comp))
            return;

        _audio.SetVolume(stream, weight > 0.001f ? engine.Volume + SharedAudioSystem.GainToVolume(weight) : float.NegativeInfinity, comp);
        IAudioSource source = comp;
        source.Pitch = Math.Clamp(1f + (audio.Rpm - Centers[index]) * engine.PitchRange * 2f, 0.7f, 1.5f);
    }

    private EntityUid? Play(EntityUid uid, SoundSpecifier? sound, float volume, float maxDistance)
    {
        var param = AudioParams.Default.WithLoop(true).WithVolume(volume).WithMaxDistance(maxDistance);
        return _audio.PlayEntity(sound, Filter.Local(), uid, false, param)?.Entity;
    }

    private void StopAll(EngineAudio audio)
    {
        for (var i = 0; i < audio.Layers.Length; i++)
        {
            audio.Layers[i] = _audio.Stop(audio.Layers[i]);
        }

        audio.Skid = _audio.Stop(audio.Skid);
    }
}
