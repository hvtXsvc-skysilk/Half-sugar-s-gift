using hvtXsvc.Core;
using Nebula.Game.Statistics;
using Nebula.Modules;
using Nebula.Player;
using NebulaN.Core;
using NebulaN.Roles.Modifier;
using Virial.Events.Player;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Roles.Impostor;

public class Owl : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{

    static internal FloatConfiguration hypnotizeCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.owl.cooldown", (5f, 60f, 2.5f), 20f,
        FloatConfigurationDecorator.Second, null, null);

    static internal IntegerConfiguration maxHypnotizes = NebulaAPI.Configurations.Configuration(
        "options.role.owl.maxUses", (1, 10), 2, null, null);

    static internal FloatConfiguration hypnotizeDuration = NebulaAPI.Configurations.Configuration(
        "options.role.owl.duration", (30f, 300f, 15f), 60f,
        FloatConfigurationDecorator.Second, null, null);

    internal static BoolConfiguration privateChatEnabled = NebulaAPI.Configurations.Configuration(
        "options.role.owl.privateChat", true, null, null);

    static internal FloatConfiguration soulEatCooldown = NebulaAPI.Configurations.Configuration(
        "options.role.owl.soulEatCooldown", (5f, 60f, 2.5f), 15f,
        FloatConfigurationDecorator.Second, null, null);

    private Owl() : base(
        "owl",
        Cor.impRed,
        RoleCategory.ImpostorRole,
        NebulaTeams.ImpostorTeam,
        new Virial.Configuration.IConfiguration[] {
            hypnotizeCooldown, maxHypnotizes, hypnotizeDuration,
            privateChatEnabled, soulEatCooldown
        }
    )
    {
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset
            .GetResource("BigPic/OwlIllustration.png")?.AsImage(115f);
    }

    Virial.Media.Image? DefinedAssignable.IconImage =>
        NebulaAPI.AddonAsset.GetResource("Smallicon/OwlIcon.png")?.AsImage();

    Citation? HasCitation.Citation => Citations.hvtXsvc_hsg;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("OwlHypnotize.png")?.AsImage(115f),
            "role.owl.doc.hypnotize"
        );
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("OwlSoulEat.png")?.AsImage(115f),
            "role.owl.doc.soulEat"
        );
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%MAXUSES%", ((int)maxHypnotizes).ToString());
        yield return new AssignableDocumentReplacement("%DUR%", ((int)hypnotizeDuration).ToString());
    }

    public static readonly Owl MyRole = new Owl();

    internal static GamePlayer? ActiveHypnotized = null;
    internal static Instance? ActiveOwl = null;

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    // ... 前面所有 using 及类定义不变，直到 Instance 类 ...

    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        void IGameOperator.OnReleased() { }
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Enumerable.Empty<IPlayerAbility>();
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        private int usesLeft;
        private ModAbilityButton? hypnotizeBtn;
        private ModAbilityButton? soulEatBtn;
        private GamePlayer? currentTarget;
        private GamePlayer? pendingHypnotizeTarget;

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) return;
            usesLeft = maxHypnotizes;
            currentTarget = null;
            pendingHypnotizeTarget = null;
            ActiveOwl = this;
            var hypnotizeIcon = NebulaAPI.AddonAsset.GetResource("OwlHypnotize.png")?.AsImage(100f);
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);

            hypnotizeBtn = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Ability,
                hypnotizeCooldown,
                "owl.hypnotize",
                hypnotizeIcon,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null
                    && playerTracker.CurrentTarget != MyPlayer
                    && !playerTracker.CurrentTarget.IsImpostor
                    && pendingHypnotizeTarget == null
                    && currentTarget == null,
                (button) => !MyPlayer.IsDead && usesLeft > 0,
                false
            );
            hypnotizeBtn.ShowUsesIcon(4, usesLeft.ToString());

            hypnotizeBtn.OnClick = (button) =>
            {
                var target = playerTracker.CurrentTarget;
                if (target == null || pendingHypnotizeTarget != null || currentTarget != null) return;

                pendingHypnotizeTarget = target;
                usesLeft--;
                button.UpdateUsesIcon(usesLeft.ToString());
                button.StartCoolDown();
            };
            var soulEatIcon = NebulaAPI.AddonAsset.GetResource("OwlSoulEat.png")?.AsImage(100f);
            soulEatBtn = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.SecondaryAbility,
                soulEatCooldown,
                "owl.soulEat",
                soulEatIcon,
                (ModAbilityButton _) => currentTarget != null && !currentTarget.IsDead,
                (button) => !MyPlayer.IsDead && currentTarget != null && !currentTarget.IsDead,
                false
            );
            soulEatBtn.OnClick = (button) =>
            {
                if (currentTarget == null || currentTarget.IsDead) return;
                MyPlayer.MurderPlayer(currentTarget, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill);
                ClearHypnotized();
                button.StartCoolDown();
            };
            GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
            {
                if (!AmOwner || currentTarget == null || currentTarget.IsDead) return;

                var meetingBtn = NebulaAPI.Modules.AbilityButton(
                    this, MyPlayer,
                    VirtualKeyInput.None,
                    0f,
                    "owl.soulEatMeeting",
                    soulEatIcon,
                    (ModAbilityButton _) => true,
                    (button) => !MyPlayer.IsDead && currentTarget != null && !currentTarget.IsDead,
                    true
                );
                meetingBtn.OnClick = (button) =>
                {
                    if (currentTarget == null || currentTarget.IsDead) return;
                    MyPlayer.MurderPlayer(currentTarget, PlayerState.Dead, EventDetail.Kill, KillParameter.MeetingKill, KillCondition.BothAlive);
                    ClearHypnotized();
                };
            }, this);
            GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev =>
            {
                if (!AmOwner || pendingHypnotizeTarget == null) return;

                if (!pendingHypnotizeTarget.IsDead && MyPlayer.IsAlive)
                {
                    pendingHypnotizeTarget.AddModifier(Hypnotized.MyRole);
                    currentTarget = pendingHypnotizeTarget;
                    ActiveHypnotized = currentTarget;
                }
                pendingHypnotizeTarget = null;
            }, this);
            GameOperatorManager.Instance?.Subscribe<PlayerMurderedEvent>(ev =>
            {
                if (currentTarget != null && ev.Murderer == currentTarget)
                    ClearHypnotized();
            }, this);
            GameOperatorManager.Instance?.Subscribe<PlayerDieEvent>(ev =>
            {
                if (ev.Player == pendingHypnotizeTarget)
                    pendingHypnotizeTarget = null;
                if (ev.Player == currentTarget)
                    ClearHypnotized();
            }, this);
        }

        private void ClearHypnotized()
        {
            if (currentTarget != null)
            {
                currentTarget.RemoveModifier(Hypnotized.MyRole);
                currentTarget = null;
                ActiveHypnotized = null;
            }
        }

        void RuntimeAssignable.OnInactivated()
        {
            ClearHypnotized();
            pendingHypnotizeTarget = null;
        }

        public void Usurp() { }
    }
    
    internal static RemoteProcess<(byte, string)> RpcOwlChat = new(
        "OwlPrivateChat",
        (data, _) =>
        {
            var sender = GamePlayer.GetPlayer(data.Item1);
            if (sender == null) return;
            var local = GamePlayer.GetPlayer(PlayerControl.LocalPlayer.PlayerId);
            if (local == null) return;

            bool isOwl = local.Role.Role is Owl;
            bool isHypnotized = local.TryGetModifier<Hypnotized.Instance>(out Hypnotized.Instance _) && ActiveHypnotized?.PlayerId == local.PlayerId;

            if (!isOwl && !isHypnotized) return;

            var pc = PlayerControl.AllPlayerControls.GetFastEnumerator()
                .FirstOrDefault(p => p.PlayerId == sender.PlayerId);
            if (pc == null) return;

            string displayName;
            if (sender.Role.Role is Owl)
            {
                displayName = $"<color=black>{Language.Translate("owl.chatName")}</color>";
            }
            else
            {
                displayName = pc.Data.PlayerName;
            }

            string origName = pc.name;
            pc.SetName(displayName);
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(pc, data.Item2);
            pc.SetName(origName);
        });

    public static bool CanUseOwlChat(GamePlayer player)
    {
        if (!privateChatEnabled) return false;
        if (player.Role.Role is Owl) return true;
        if (player.TryGetModifier<Hypnotized.Instance>(out Hypnotized.Instance _) && ActiveHypnotized?.PlayerId == player.PlayerId)
            return true;
        return false;
    }
}

public static class OwlChatHelper
{
    public static void SendOwlChat(string message)
    {
        var sender = PlayerControl.LocalPlayer;
        Owl.RpcOwlChat.Invoke((sender.PlayerId, message));
    }
}