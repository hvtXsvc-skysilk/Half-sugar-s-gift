global using Nebula.Game;
global using Nebula.Utilities;
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Text;
global using System.Threading.Tasks;
global using UnityEngine;
global using Virial;
global using Virial.Assignable;
global using Virial.Attributes;
global using Virial.Compat;
global using Virial.Components;
global using Virial.Configuration;
global using Virial.Events.Game;
global using Virial.Events.Game.Meeting;
global using Virial.Game;
global using Citations1 = hvtXsvc.Core.Citations;
global using Color1 = Virial.Color;
global using NPlayer1 = Virial.Game.Player;
using Nebula.Modules;
using NebulaN.Core;
using Virial.Events.Player;

namespace NebulaN.Roles.Impostor;
public class Restorer : DefinedRoleTemplate, HasCitation, DefinedRole,
    RuntimeAssignableGenerator<RuntimeRole>, IAssignableDocument
{

    static private FloatConfiguration cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.restorer.cooldown",
        (5f, 60f, 2.5f),
        25f,
        FloatConfigurationDecorator.Second,
        null, null
    );

    static private IntegerConfiguration skillUses = NebulaAPI.Configurations.Configuration(
        "options.role.restorer.skillUses",
        (1, 15),
        3,
        null, null
    );


    private Restorer() : base(
        "restorer",
        Cor.impRed,
        RoleCategory.ImpostorRole,
        NebulaTeams.ImpostorTeam,
        new Virial.Configuration.IConfiguration[] { cooldown, skillUses }
    )
    {
  
        ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset
            .GetResource("BigPic/RestorerPic.png")?.AsImage(115f);
    }


    Virial.Media.Image? DefinedAssignable.IconImage =>
        NebulaAPI.AddonAsset.GetResource("Smallicon/RestorerIcon.png")?.AsImage();

  
    Citation? HasCitation.Citation => Citations1.hvtXsvc_hsg;

    
    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new AssignableDocumentImage(
            NebulaAPI.AddonAsset.GetResource("RestorerButton.png")?.AsImage(115f),
            "role.restorer.doc.image"  
        );
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new AssignableDocumentReplacement("%USES%", ((int)skillUses).ToString());
    }


    public static readonly Restorer MyRole = new Restorer();


    public RuntimeRole CreateInstance(NPlayer1 player, int[] arguments)
        => new Instance(player);


    public class Instance : RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
   
        void IGameOperator.OnReleased() { }

     
        IEnumerable<IPlayerAbility> RuntimeAssignable.MyAbilities => Array.Empty<IPlayerAbility>();
        int usesLeft;
        bool usedRestore = false;
        List<byte> restoredPlayers = new List<byte>();

        public Instance(NPlayer1 player) : base(player) { }
        public DefinedRole Role => MyRole;

        Dictionary<byte, DefinedRole> pendingRestore = new Dictionary<byte, DefinedRole>();

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                usesLeft = skillUses;
                var playerTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
                playerTracker.SetColor(MyRole.RoleColor);

                Virial.Media.Image icon = NebulaAPI.AddonAsset.GetResource("RestorerButton.png")?.AsImage(100f);

                ModAbilityButton btn = NebulaAPI.Modules.AbilityButton(
                    this, base.MyPlayer,
                    Virial.Compat.VirtualKeyInput.Ability,
                    cooldown,
                    "restorer",
                    icon,
                    (ModAbilityButton _) => playerTracker.CurrentTarget != null && playerTracker.CurrentTarget != MyPlayer,
                    (button) => !MyPlayer.IsDead && usesLeft > 0,
                    false
                );

                btn.OnClick = (button) =>
                {
                    var target = playerTracker.CurrentTarget;
                    if (target == null) return;

                    DefinedRole? newRole = null;
                    switch (target.Role.Role.Category)
                    {
                        case RoleCategory.CrewmateRole:
                            newRole = NebulaAPI.Assignables.GetRole("crewmate");
                            break;
                        case RoleCategory.ImpostorRole:
                            newRole = NebulaAPI.Assignables.GetRole("impostor");
                            break;
                        case RoleCategory.NeutralRole:
                            newRole = NebulaAPI.Assignables.GetRole("jester");
                            break;
                    }

                    if (newRole != null)
                    {
                        pendingRestore[target.PlayerId] = newRole;
                      
                        if (target.Role.Role is Restorer)
                            new StaticAchievementToken("restorer.challenge.same");
                    }

                    usesLeft--;
                    usedRestore = true;
                    button.StartCoolDown();
                };

                btn.OnUpdate = (button) =>
                {
                    button.ShowUsesIcon(3, usesLeft.ToString());
                };
            }
        }

       
        [Local]
        private void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (!AmOwner || pendingRestore.Count == 0) return;

            foreach (var kv in pendingRestore)
            {
                var player = NPlayer1.GetPlayer(kv.Key);
                if (player != null && !player.IsDead)
                {
                    player.SetRole(kv.Value, null);
                    restoredPlayers.Add(kv.Key);
                    RpcTriggerRestored.Invoke(kv.Key);
                }
            }

        
            var allOthers = NPlayer1.AllPlayers
                .Where(p => !p.IsDead && p.PlayerId != MyPlayer.PlayerId)
                .Select(p => p.PlayerId)
                .OrderBy(id => id);
            if (allOthers.SequenceEqual(restoredPlayers.OrderBy(id => id)))
                new StaticAchievementToken("restorer.challenge.all");

            pendingRestore.Clear();
        }


        [Local]
        private void OnDead(PlayerDieEvent ev)
        {
           if (ev.Player.AmOwner && !usedRestore)
                new StaticAchievementToken("restorer.common.dead");
        }

     
        [Local]

        private void OnGameEnd(GameEndEvent ev)
        {
           
            if (AmOwner)
            {
                if (ev.EndState.Winners.Test(MyPlayer))
                {
                    new StaticAchievementToken("restorer.common.win");
                    if (!usedRestore)
                        new StaticAchievementToken("restorer.challenge.unused");
                }
            }

            foreach (var id in restoredPlayers)
            {
                var p = NPlayer1.GetPlayer(id);
                if (p != null && p.Role.Role.LocalizedName == "jester" &&
                    ev.EndState.Winners.Test(p))
                {
                    
                    if (AmOwner)
                    {
                        new StaticAchievementToken("restorer.challenge.jesterWin");
                    }
                }
            }
        }

    
        static RemoteProcess<byte> RpcTriggerRestored = new RemoteProcess<byte>(
            "RestorerRestored",
            (targetId, _) =>
            {
                if (NPlayer1.LocalPlayer.PlayerId == targetId)
                {
                    new StaticAchievementToken("restorer.common.restored");
                }
            }
        );

    }
}