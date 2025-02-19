// New Frontiers - This file is licensed under AGPLv3
// Copyright (c) 2024 New Frontiers Contributors
// See AGPLv3.txt for details.
using Content.Server._NF.Station.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Shared._NF.Shuttles.Events;
using Robust.Shared.Physics.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private const float SpaceFrictionStrength = 0.0015f;
    private const float AnchorDampeningStrength = 0.5f;
    private void NfInitialize()
    {
        SubscribeLocalEvent<ShuttleConsoleComponent, SetInertiaDampeningRequest>(OnSetInertiaDampening);
    }

    private void OnSetInertiaDampening(EntityUid uid, ShuttleConsoleComponent component, SetInertiaDampeningRequest args)
    {
        // Ensure that the entity requested is a valid shuttle (stations should not be togglable)
        if (!EntityManager.TryGetComponent(uid, out TransformComponent? transform) ||
            !transform.GridUid.HasValue ||
            !EntityManager.TryGetComponent(transform.GridUid, out PhysicsComponent? physicsComponent) ||
            !EntityManager.TryGetComponent(transform.GridUid, out ShuttleComponent? shuttleComponent))
            return;

        if (args.Mode == InertiaDampeningMode.Query)
        {
            _console.RefreshShuttleConsoles(transform.GridUid.Value);
            return;
        }

        if (!EntityManager.HasComponent<ShuttleComponent>(transform.GridUid) ||
            EntityManager.TryGetComponent<StationDataComponent>(_station.GetOwningStation(transform.GridUid), out var stationData)
            && stationData.StationConfig != null)
            return;

        var linearDampeningStrength = args.Mode switch
        {
            InertiaDampeningMode.Off => SpaceFrictionStrength,
            InertiaDampeningMode.Dampen => shuttleComponent.LinearDamping,
            InertiaDampeningMode.Anchor => AnchorDampeningStrength,
            _ => shuttleComponent.LinearDamping, // other values: default to some sane behaviour (assume normal dampening)
        };

        var angularDampeningStrength = args.Mode switch
        {
            InertiaDampeningMode.Off => SpaceFrictionStrength,
            InertiaDampeningMode.Dampen => shuttleComponent.AngularDamping,
            InertiaDampeningMode.Anchor => AnchorDampeningStrength,
            _ => shuttleComponent.AngularDamping, // other values: default to some sane behaviour (assume normal dampening)
        };

        _physics.SetLinearDamping(transform.GridUid.Value, physicsComponent, linearDampeningStrength);
        _physics.SetAngularDamping(transform.GridUid.Value, physicsComponent, angularDampeningStrength);
        _console.RefreshShuttleConsoles(transform.GridUid.Value);
    }

    public InertiaDampeningMode NfGetInertiaDampeningMode(EntityUid entity)
    {
        if (!EntityManager.TryGetComponent<TransformComponent>(entity, out var xform))
            return InertiaDampeningMode.Dampen;

        var owningStation = _station.GetOwningStation(xform.GridUid);

        // Not a shuttle, shouldn't be togglable
        if (!EntityManager.HasComponent<ShuttleComponent>(xform.GridUid) ||
            EntityManager.TryGetComponent<StationDataComponent>(owningStation, out var stationData)
            && stationData.StationConfig != null)
            return InertiaDampeningMode.Station;

        if (!EntityManager.TryGetComponent(xform.GridUid, out PhysicsComponent? physicsComponent))
            return InertiaDampeningMode.Dampen;

        if (physicsComponent.LinearDamping >= AnchorDampeningStrength)
            return InertiaDampeningMode.Anchor;

        if (physicsComponent.LinearDamping <= SpaceFrictionStrength)
            return InertiaDampeningMode.Off;

        return InertiaDampeningMode.Dampen;
    }

}
