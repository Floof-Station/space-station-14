using Content.Shared._TP.Kitchen.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._TP.Kitchen.Systems;

public sealed partial class CoffeeBasketSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> StirrerTag = "CoffeeStirrer";
    private static readonly ProtoId<TagPrototype> TamperTag = "CoffeeTamper";

    [SubscribeLocalEvent]
    private void OnExamined(Entity<CoffeeBasketComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Prep.HasFlag(CoffeePrep.Stirred))
            args.PushMarkup(Loc.GetString("coffee-basket-examine-stirred"));

        if (ent.Comp.Prep.HasFlag(CoffeePrep.Tamped))
            args.PushMarkup(Loc.GetString("coffee-basket-examine-tamped"));
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<CoffeeBasketComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tag.HasTag(args.Used, StirrerTag) && !_tag.HasTag(args.Used, TamperTag))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.PrepTime, new CoffeePrepDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnPrepDoAfter(Entity<CoffeeBasketComponent> ent, ref CoffeePrepDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        if (_tag.HasTag(used, StirrerTag))
        {
            ent.Comp.Prep = CoffeePrep.Stirred;
            _popup.PopupClient(Loc.GetString("coffee-basket-stirred"), ent, args.User);
        }
        else if (_tag.HasTag(used, TamperTag))
        {
            ent.Comp.Prep |= CoffeePrep.Tamped;
            _popup.PopupClient(Loc.GetString("coffee-basket-tamped"), ent, args.User);
        }

        Dirty(ent);
        args.Handled = true;
    }
}
