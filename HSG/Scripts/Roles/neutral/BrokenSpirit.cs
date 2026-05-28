using hvtXsvc.Core;
using Nebula.Game.Statistics;
using Nebula.Player;
using NebulaN.Core;
using NebulaN.Roles.Crewmate;
using System.Collections.Generic;
using Virial;
using Virial.Events.Player;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Roles.Neutral;

public class BrokenSpirit : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{
    static FloatConfiguration KillCoolDown = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.BSKillCoolDown",
        (2f, 60f, 2f),
        25f,
        FloatConfigurationDecorator.Second,
        null, null
    );

    static IntegerConfiguration BSKillMaxUses = NebulaAPI.Configurations.Configuration(
        "options.role.spirit.KillMaxUses",
        (1, 20),
        3,
        null, null
    );
    bool ISpawnable.IsSpawnable => false;
    private BrokenSpirit() : base(
           "brokenspirit",
           Cor.SpiritCor,
           RoleCategory.NeutralRole,
           team.SpiritTeam,
            new Virial.Configuration.IConfiguration[]
            {
            KillCoolDown, BSKillMaxUses
           },
            false, true, () => false
       )
    {
    }

    bool DefinedAssignable.ShowOnHelpScreen => true;
    Citation HasCitation.Citation => Citations.hvtXsvc_hsg;
    public static readonly BrokenSpirit MyRole = new BrokenSpirit();

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments)
    {
        int initKills = (arguments != null && arguments.Length > 0) ? arguments[0] : (int)BSKillMaxUses;
        byte spiritId = (arguments != null && arguments.Length > 1) ? (byte)arguments[1] : byte.MaxValue;
        return new Instance(player, initKills, spiritId);
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole,
        RuntimeAssignable, ILifespan, IReleasable, IBindPlayer, IGameOperator
    {
        int remainingKills;
        byte mySpiritId;
        ModAbilityButton? Kill;
        public Instance(GamePlayer player, int kills, byte spiritId) : base(player)
        {
            remainingKills = kills;
            mySpiritId = spiritId;
        }
        public DefinedRole Role => MyRole;
        void RuntimeAssignable.OnActivated()
        {
            if (!AmOwner) { return; }
            Virial.Media.Image SpKillI = NebulaAPI.AddonAsset.GetResource("RefereeRecruit.png")?.AsImage(100f);
            var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
            playerTracker.SetColor(MyRole.RoleColor);
            Kill = NebulaAPI.Modules.AbilityButton(
                this, MyPlayer,
                VirtualKeyInput.Ability,
                KillCoolDown,
                "brokenspirit.bpKill",
                SpKillI,
                (ModAbilityButton _) => playerTracker.CurrentTarget != null && playerTracker.CurrentTarget != MyPlayer && !(playerTracker.CurrentTarget.Role is Spirit.Instance),
                (button) => !MyPlayer.IsDead && remainingKills > 0,
                false
            );
            Kill.ShowUsesIcon(4, remainingKills.ToString());
            Kill.OnClick = (button) =>
            {
                var target = playerTracker.CurrentTarget;
                if (target == null || target == MyPlayer || remainingKills <= 0) return;
                remainingKills--;
                MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, KillParameter.NormalKill, KillCondition.NormalKill);
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
    }
}