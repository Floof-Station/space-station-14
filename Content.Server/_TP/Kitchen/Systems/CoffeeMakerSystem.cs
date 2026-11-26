using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Shared._TP.Kitchen.Components;
using Content.Shared._TP.Kitchen.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._TP.Kitchen.Systems;

/// <summary>
///     Makes an entity... make coffee. Idunno how else to describe this.
/// </summary>
public sealed partial class CoffeeMakerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<TagPrototype> PitcherTag = "Pitcher";

    [SubscribeLocalEvent]
    private void OnEntInserted(Entity<CoffeeMakerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        switch (args.Container.ID)
        {
            case "Beaker":
                var visual = _tag.HasTag(args.Entity, PitcherTag) ? CoffeeMakerVisuals.Pitcher : CoffeeMakerVisuals.Beaker;
                _appearance.SetData(ent.Owner, visual, true);
                break;
            case "Basket":
                _appearance.SetData(ent.Owner, CoffeeMakerVisuals.Basket, true);
                break;
        }
    }

    [SubscribeLocalEvent]
    private void OnEntRemoved(Entity<CoffeeMakerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        switch (args.Container.ID)
        {
            case "Beaker":
                _appearance.SetData(ent.Owner, CoffeeMakerVisuals.Pitcher, false);
                _appearance.SetData(ent.Owner, CoffeeMakerVisuals.Beaker, false);
                break;
            case "Basket":
                _appearance.SetData(ent.Owner, CoffeeMakerVisuals.Basket, false);
                break;
        }
    }

    /// <summary>
    ///     Verbs for the coffee maker.
    /// </summary>
    /// <param name="ent">CoffeeMaker entity</param>
    /// <param name="args">AlternativeVerb arguments</param>
    [SubscribeLocalEvent]
    private void OnActivationVerb(Entity<CoffeeMakerComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.BrewEnd == null)
        {
            var user = args.User;
            var verb = new ActivationVerb
            {
                Text = Loc.GetString("coffee-maker-verb-enable"),
                Act = () => AttemptEnable(ent, user),
            };

            args.Verbs.Add(verb);
        }
    }

    /// <summary>
    ///     Called when we want to attempt to enable the coffee maker.
    /// </summary>
    /// <param name="ent">CoffeeMaker entity</param>
    /// <param name="user">User uid</param>
    private void AttemptEnable(Entity<CoffeeMakerComponent> ent, EntityUid user)
    {
        if (!CanBrew(ent, user))
            return;

        ent.Comp.BrewEnd = _timing.CurTime + ent.Comp.BrewTime;
        ent.Comp.PlayingStream = _audio.PlayPvs(ent.Comp.BrewSound, ent)?.Entity;
        _appearance.SetData(ent.Owner, CoffeeMakerVisuals.Brewing, true);
        _itemSlots.SetLock(ent.Owner, "Beaker", true);
        _itemSlots.SetLock(ent.Owner, "Basket", true);
        _popup.PopupEntity(Loc.GetString("coffee-maker-message-enabled", ("entity", ent.Owner)), ent.Owner, user);
        _popup.PopupEntity(Loc.GetString("coffee-maker-message-enabled-other",
                ("user", user),
                ("entity", ent.Owner)),
            ent.Owner,
            Filter.PvsExcept(user),
            true);
    }

    private bool CanBrew(Entity<CoffeeMakerComponent> ent, EntityUid user)
    {
        // And finally, before finishing up properly, we check if it's powered.
        if (!_power.IsPowered(ent))
        {
            _popup.PopupEntity(Loc.GetString("coffee-maker-message-unpowered", ("entity", ent.Owner)), user, user);
            return false;
        }

        // First, we check for each container if it has items in it.
        // If any of them return null or empty, we return and display a popup message.
        if (!_container.TryGetContainer(ent.Owner, "Beaker", out var beakerContainer) || beakerContainer.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("coffee-maker-message-no-beaker"), user, user);
            return false;
        }

        if (!_container.TryGetContainer(ent.Owner, "Basket", out var basketContainer) || basketContainer.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("coffee-maker-message-no-basket"), user, user);
            return false;
        }

        return true;
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<CoffeeMakerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && ent.Comp.BrewEnd != null)
            StopBrewing(ent);
    }

    private void StopBrewing(Entity<CoffeeMakerComponent> ent)
    {
        ent.Comp.BrewEnd = null;
        ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
        _appearance.SetData(ent.Owner, CoffeeMakerVisuals.Brewing, false);
        _itemSlots.SetLock(ent.Owner, "Beaker", false);
        _itemSlots.SetLock(ent.Owner, "Basket", false);
    }

    private CoffeeQuality GetQuality(EntityUid basketEnt)
    {
        if (!TryComp<CoffeeBasketComponent>(basketEnt, out var basket))
            return CoffeeQuality.Low;

        return basket.Prep switch
        {
            CoffeePrep.Stirred | CoffeePrep.Tamped => CoffeeQuality.High,
            CoffeePrep.None => CoffeeQuality.Low,
            _ => CoffeeQuality.Medium,
        };
    }

    /// <summary>
    ///     The main update loop for the coffee maker.
    /// </summary>
    /// <param name="frameTime">Current frame timing (I guess?)</param>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CoffeeMakerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // If the coffee maker isn't enabled we don't do anything.
            if (comp.BrewEnd is not { } brewEnd || _timing.CurTime < brewEnd)
                continue;

            StopBrewing((uid, comp));

            if (_itemSlots.GetItemOrNull(uid, "Beaker") is { } beakerEnt
                && _itemSlots.GetItemOrNull(uid, "Basket") is { } basketEnt)
            {
                // First we get the solutions of the basket and beaker
                // Afterward, we remove the grounds from the basket and add a coffee reagent to the beaker.
                // These depend on the grounds used, see Brews in the CoffeeMaker prototype.
                if (!_solution.TryGetDrainableSolution(basketEnt, out var basketSolution, out _))
                    continue;

                if (!_solution.TryGetRefillableSolution(beakerEnt, out var beakerSolution, out _))
                    continue;

                var quality = GetQuality(basketEnt);

                foreach (var reagent in basketSolution.Value.Comp.Solution.Contents.ToArray())
                {
                    // Ground removal and coffee adding processes
                    _solution.RemoveReagent(basketSolution.Value, reagent);

                    var recipe = _proto.EnumeratePrototypes<CoffeeMakerRecipePrototype>()
                        .FirstOrDefault(recipe => recipe.Input == reagent.Reagent.Prototype);

                    if (recipe == null)
                    {
                        _solution.TryAddReagent(beakerSolution.Value, reagent, out _);
                        continue;
                    }

                    // ReSharper disable EnforceForeachStatementBraces, EnforceIfStatementBraces
                    // I'm not gonna expand this code with braces, fuck you rider
                    foreach (var (output, amount) in recipe.Outputs)
                        _solution.TryAddReagent(beakerSolution.Value, new ReagentId(output, null), reagent.Quantity * amount, out _);

                    if (recipe.Quality?.GetValueOrDefault(quality) is { } qualityOutputs)
                        foreach (var (output, amount) in qualityOutputs)
                            _solution.TryAddReagent(beakerSolution.Value, new ReagentId(output, null), reagent.Quantity * amount, out _);

                    foreach (var (byproduct, amount) in recipe.Byproducts)
                        _solution.TryAddReagent(basketSolution.Value, new ReagentId(byproduct, null), reagent.Quantity * amount, out _);
                    // ReSharper restore EnforceForeachStatementBraces, EnforceIfStatementBraces
                }

                if (TryComp<CoffeeBasketComponent>(basketEnt, out var basket))
                {
                    basket.Prep = CoffeePrep.None;
                    Dirty(basketEnt, basket);
                }

                // Finally, we reset the maker, play a sound, and display a popup message.
                _audio.PlayPvs(comp.FinishSound, uid, AudioParams.Default.WithVolume(-3));
                _popup.PopupEntity(Loc.GetString("coffee-maker-message-complete"), uid, PopupType.Medium);
            }
        }
    }
}
