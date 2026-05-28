using Nebula.Modules;
using Nebula.Roles;
using NebulaN.Core;
using NebulaN.Roles.Neutral;
using Virial.Events.Player;
using GamePlayer = Virial.Game.Player;
using Image = Virial.Media.Image;
using RolesAll = Nebula.Roles.Roles;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace NebulaN.Roles.Modifier;

public class ImaginationModifier : DefinedModifierTemplate, DefinedModifier
{
    private ImaginationModifier() : base(
        "imagimodi",
        new Virial.Color(128, 128, 128, byte.MaxValue),
        null,
        false,
        () => false)
    { }
    bool DefinedModifier.IsMadmate => true;
    public static ImaginationModifier MyRole = new();

    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments)
        => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public Instance(GamePlayer player) : base(player) { }

        private FlexibleLifespan? abilityLifespan;
        private bool eventsRegistered = false;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;

            if (!eventsRegistered)
            {
                eventsRegistered = true;
                GameOperatorManager.Instance?.Subscribe<EndCriteriaMetEvent>(ev =>
                {
                    if (!MyPlayer.IsDead)
                        ev.TryOverwriteEnd(Imagination.ImaginationWin, 1, 0);
                }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerCheckWinEvent>(ev =>
                {
                    if (!MyPlayer.IsDead && ev.GameEnd == Imagination.ImaginationWin)
                        ev.IsWin = true;
                }, this);
                GameOperatorManager.Instance?.Subscribe<PlayerBlockWinEvent>(ev =>
                {
                    if (!MyPlayer.IsDead)
                        ev.SetBlockedIf(ev.GameEnd != Imagination.ImaginationWin);
                }, this);
                GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev =>
                {
                    if (!MyPlayer.IsDead) ShowImaginationWindow();
                }, this);

            }
        }

        private void ShowImaginationWindow()
        {
            var candidates = RolesAll.AllRoles
                .Where(r => Imagination.roleFilterOption.Contains(r) && r.IsSpawnable &&
                            r is not Imagination && !Imagination.Instance.SelectedRoleIds.Contains(r.Id))
                .ToList();

            if (candidates.Count == 0)
            {
                MyPlayer.Suicide(State.Depression, null, KillParameter.NormalKill, null);
                return;
            }

            int count = Math.Min(Imagination.candidateCount, candidates.Count);
            var displayedRoles = candidates.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();

            var window = MetaScreen.GenerateWindow(
                new Vector2(7.6f, 4.2f),
                HudManager.Instance.transform,
                new Vector3(0, 0, -50f),
                true, false, withCloseButton: false);

            MetaWidgetOld widget = new();
            MetaWidgetOld inner = new();

            foreach (var r in displayedRoles)
            {
                var role = r;
                inner.Append(new MetaWidgetOld.Button(() =>
                {
                    Imagination.Instance.SelectedRoleIds.Add(role.Id);
                    MyPlayer.SetRole(role, role.DefaultAssignableArguments);
                    window.CloseScreen();
                }, TextAttributeOld.BoldAttr)
                {
                    RawText = role.DisplayColoredName,
                    PostBuilder = (button, renderer, text) =>
                    {
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        button.transform.localPosition += new Vector3(0.05f, 0f, 0f);
                        text.transform.localPosition += new Vector3(0.072f, 0f, 0f);
                        SpriteRenderer spriteRenderer = UnityHelper.CreateObject<SpriteRenderer>("Icon",
                            button.transform, new Vector3(-0.65f, 0f, -0.1f), null);
                        Image roleIcon = role.GetRoleIcon();
                        spriteRenderer.sprite = GetSpriteFromImage(roleIcon);
                        spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        spriteRenderer.material = RoleIcon.GetRoleIconMaterial(role, 0.8f, null);
                        spriteRenderer.transform.localScale = new Vector3(0.253f, 0.253f, 1f);
                        spriteRenderer.SetBothOrder(15);
                    }
                });
            }

            MetaWidgetOld.ScrollView scroller = new(new Vector2(6.9f, 3.8f), inner, true)
            { Alignment = IMetaWidgetOld.AlignmentOption.Center };
            widget.Append(scroller);
            widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttr)
            {
                MyText = new RawTextComponent(Language.Translate("role.imagination.select")),
                Alignment = IMetaWidgetOld.AlignmentOption.Center
            });
            window.SetWidget(widget);
        }
        private static Sprite? GetSpriteFromImage(Virial.Media.Image? image)
        {
            if (image == null) return null;
            var method = typeof(Virial.Media.Image).GetMethod("GetSprite", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return method?.Invoke(image, null) as Sprite;
        }
        string RuntimeAssignable.OverrideRoleName(string lastRoleName, bool isShort, bool canSeeAllInfo)
        {
            if (canSeeAllInfo || AmOwner)
            {
                var currentRole = MyPlayer.Role.Role;
                if (currentRole is not Imagination)
                    return Language.Translate("role.imagination.prefix")
                        .Replace("%ROLE%", currentRole.DisplayColoredName);
            }
            return null;
        }
    }
}