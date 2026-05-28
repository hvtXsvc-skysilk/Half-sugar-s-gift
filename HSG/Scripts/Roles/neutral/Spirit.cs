using hvtXsvc.Core;
using Nebula.Modules;
using Nebula.Modules.ScriptComponents;
using NebulaN.Core;
using System.Collections.Generic;
using Virial;
using Virial.Events.Player;
using GamePlayer = Virial.Game.Player;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using System.Linq;
using NebulaN.Scripts.Roles.crewmate;

namespace NebulaN.Roles.Neutral;

public class Spirit : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    private static FloatConfiguration KillCoolDown = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.KillCoolDown",
        (2f, 60f, 2f),
        25f,
        FloatConfigurationDecorator.Second
    );

    private static IntegerConfiguration KillMaxUses = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.KillMaxUses",
        (1, 20),
        3
    );
    private static BoolConfiguration SoulBack = NebulaAPI.Configurations.Configuration(
    "options.role.spirit.SoulBack",
    true
);
    private static FloatConfiguration sbCoolDown = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.sbCoolDown",
        (5f, 60f, 2f),
        15f,
        FloatConfigurationDecorator.Second
    );
    static private IntegerConfiguration convertSpiritStrikes = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.convertStrikes",
        (1, 15),
        3
    );
    static private IntegerConfiguration shardKillCount = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.shardKillCount",
        (0, 20),
        1
    );

    private Spirit() : base(
           "spirit",
           Cor.SpiritCor,
           RoleCategory.NeutralRole,
           team.SpiritTeam,
            new Virial.Configuration.IConfiguration[]
            {
            KillCoolDown, KillMaxUses,SoulBack
           }
       )
    {

    }

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    public static IConfigurationHolder? GetConfig() => MyRole.ConfigurationHolder;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource(null)?.AsImage(115f),
            "role.spirit.doc.spkill"
        );
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("SoulBack.png")?.AsImage(115f),
            "role.spirit.doc.sb"
        );
    }

    Citation HasCitation.Citation => Citations.hvtXsvc_hsg;
    public static readonly Spirit MyRole = new Spirit();

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments)
    {
        int initStrikes = (arguments != null && arguments.Length > 0) ? arguments[0] : (int)KillMaxUses;
        return new Instance(player, initStrikes);
    }

    static private RemoteProcess<(byte oldSpiritId, byte newSpiritId, int newSpiritStrikes, int shardKills)> RpcConvertSpirit = new(
    "SpiritConvert",
    (message, _) =>
    {
        var oldSpirit = GamePlayer.GetPlayer(message.oldSpiritId);
        var newSpirit = GamePlayer.GetPlayer(message.newSpiritId);
        if (oldSpirit == null || newSpirit == null) return;
        if (oldSpirit.IsDead || newSpirit.IsDead) return;
        newSpirit.SetRole(Spirit.MyRole, new int[] { message.newSpiritStrikes });
        oldSpirit.SetRole(BrokenSpirit.MyRole, new int[] { message.shardKills, message.newSpiritId });
        var newSpiritInst = newSpirit.Role as Spirit.Instance;
        var newShardInst = oldSpirit.Role as BrokenSpirit.Instance;
        if (newSpiritInst != null)
        {
            newSpiritInst.AddShard(oldSpirit.PlayerId);
        }
        if (newShardInst != null && newShardInst.MyPlayer.AmOwner)
        {
            AmongUsUtil.PlayQuickFlash(new UnityEngine.Color(0, 0.1f, 0.4f));
        }
    }
    );
    static private RemoteProcess<byte> RpcFlashForWish = new(
    "SpiritFlashWish",
    (wishId, _) =>
    {
        if (PlayerControl.LocalPlayer.PlayerId == wishId)
            AmongUsUtil.PlayQuickFlash(new UnityEngine.Color(1f, 0.9f, 0.6f, 0.5f));
    }
);
    DefinedRole[] DefinedRole.AdditionalRoles => new[] { BrokenSpirit.MyRole };

    public class Instance : RuntimeAssignableTemplate, RuntimeRole,
        RuntimeAssignable, ILifespan, IReleasable, IBindPlayer, IGameOperator
    {
        int UsesLeft;
        bool canback;
        bool hasback;
        ModAbilityButton? SpKill;
        ModAbilityButton? sb;
        List<byte> shardIds;

        public Instance(GamePlayer myPlayer, int initStrikes = -1) : base(myPlayer)
        {
            if (initStrikes == -1) initStrikes = KillMaxUses;
            UsesLeft = initStrikes;
        }
        public void AddShard(byte shardId)
        {
            if (!shardIds.Contains(shardId))
                shardIds.Add(shardId);
        }

        public DefinedRole Role => MyRole;

        private bool IsTargetMarkedByWish(GamePlayer target, out Wish.Instance wishInstance)
        {
            wishInstance = null;
            foreach (var p in GamePlayer.AllPlayers)
            {
                if (p.Role is Wish.Instance wish && wish.MarkedPlayer == target)
                {
                    wishInstance = wish;
                    return true;
                }
            }
            return false;
        }

        private void PlayFlashForWishAndSpirit(GamePlayer wishPlayer)
        {
            AmongUsUtil.PlayQuickFlash(new UnityEngine.Color(0f, 0.1f, 0.4f));
            RpcFlashForWish.Invoke(wishPlayer.PlayerId);
        }

        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) { return; }
            UsesLeft = KillMaxUses;
            canback = SoulBack;
            hasback = false;
            shardIds = new List<byte>();
            //Virial.Media.Image SpKillI = NebulaAPI.AddonAsset.GetResource("RefereeRecruit.png")?.AsImage(100f);
            Virial.Media.Image sbI = NebulaAPI.AddonAsset.GetResource("SoulBack.png")?.AsImage(100f);
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);

            SpKill = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Kill,
                KillCoolDown,
                "spirit.kill",
                null,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null && playerTracker.CurrentTarget != MyPlayer && !(playerTracker.CurrentTarget.Role is BrokenSpirit.Instance),
                (button) => !MyPlayer.IsDead && UsesLeft > 0,
                false
            );
            SpKill.ShowUsesIcon(4, UsesLeft.ToString());

            sb = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer, VirtualKeyInput.Ability, sbCoolDown, "spirit.sb", 
                sbI,
                (ModAbilityButton _) => UsesLeft == 0 && playerTracker.CurrentTarget != null && playerTracker.CurrentTarget != MyPlayer && !(playerTracker.CurrentTarget.Role is Spirit.Instance) && !(playerTracker.CurrentTarget.Role is BrokenSpirit.Instance),
                (button) => !MyPlayer.IsDead && canback && !hasback,
                false
            );

            SpKill.OnClick = (button) =>
            {
                var target = playerTracker.CurrentTarget;
                if (target == null || target == MyPlayer || UsesLeft <= 0) return;
                UsesLeft--;
                SpKill.UpdateUsesIcon(UsesLeft.ToString());
                if (IsTargetMarkedByWish(target, out var wishInst))
                {
                    PlayFlashForWishAndSpirit(wishInst.MyPlayer);
                }
                target.Suicide(State.SanLing, null, KillParameter.NormalKill);
                button.StartCoolDown();
            };
            sb.OnClick = (button) =>
            {
                if (hasback) return;
                var target = playerTracker.CurrentTarget;
                if (target == null || target == MyPlayer) return;
                RpcConvertSpirit.Invoke((
                    MyPlayer.PlayerId,
                    target.PlayerId,
                    (int)convertSpiritStrikes,
                    (int)shardKillCount
                ));
                hasback = true;
                button.StartCoolDown();
            };
        }

        [Local]
        private void AddName(PlayerDecorateNameEvent ev)
        {
            if (!AmOwner) return;
            if (MyPlayer.IsDead) return;
            if (!(MyPlayer.Role is Spirit.Instance || MyPlayer.Role is BrokenSpirit.Instance)) return;
            if (ev.Player.Role is Spirit.Instance || ev.Player.Role is BrokenSpirit.Instance)
            {
                ev.Name += " }{".Color(new UnityEngine.Color(0f, 0.1f, 0.4f));
            }
        }

        [Local]
        private void Win(EndCriteriaMetEvent ev)
        {
            var allAlive = GamePlayer.AllPlayers.Where(p => !p.IsDead).ToList();
            var spiritTeam = allAlive.Where(p => p.Role is Spirit.Instance || p.Role is BrokenSpirit.Instance).ToList();
            var others = allAlive.Except(spiritTeam).ToList();
            bool noKillerInOthers = !others.Any(p => p.Role.Role.IsKiller);
            if (noKillerInOthers && spiritTeam.Count >= others.Count)
            {
                ev.TryOverwriteEnd(team.SpiritWin, 80, 0);
            }
        }

        [Local]
        private void CheckWin(PlayerCheckWinEvent ev)
        {
            if (ev.GameEnd == team.SpiritWin)
                ev.IsWin = true;
        }

        [OnlyHost]
        [Local]
        private void ifPlayerDie(PlayerDieEvent ev)
        {
            if (ev.Player.Role is Spirit.Instance)
            {
                foreach (var p in GamePlayer.AllPlayers.Where(p => !p.IsDead && p.Role is BrokenSpirit.Instance))
                {
                    p.Suicide(State.SoulBack, null, KillParameter.NormalKill);
                }
            }
        }
    }
}