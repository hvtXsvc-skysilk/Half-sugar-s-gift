using hvtXsvc.Core;
using Nebula.Modules;
using Nebula.Player;
using Nebula.Roles.Abilities;
using Nebula.Roles.Complex;
using Nebula.Utilities;
using NebulaN.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Virial;
using Virial.Events.Game;
using Virial.Events.Player;
using GamePlayer = Virial.Game.Player;

namespace NebulaN.Roles.Crewmate;

public class Snitch : DefinedRoleTemplate, HasCitation, DefinedRole,IAssignableDocument
{
    static BoolConfiguration CanBeGuess = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.canBeGuess",
         false
        );

    static ValueConfiguration<int> ZhixiangJiGeLang = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.zhixiangJiGeLang",
        new[] { "options.role.snitch.all", "1", "2", "3", "4", "5" },
        1
        );
    static BoolConfiguration UseSpeTask = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.useSpeTask",
        false
        );
    static IntegerConfiguration TaskShuLiang = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.TaskShuLiang",
        (1, 15),
        7,
        () => UseSpeTask
        );
    static ValueConfiguration<int> NeCo = NebulaAPI.Configurations.Configuration(
        "options.role.snitch.NeCo",
        new[] {
            "options.role.snitch.neutral.any",
            "options.role.snitch.neutral.OnlyHUAIDE",
            "options.role.snitch.neutral.none"
        },
        2
        );


    private Snitch() : base(
        "snitch",
        Cor.green,
        RoleCategory.CrewmateRole,
        NebulaTeams.CrewmateTeam,
        new Virial.Configuration.IConfiguration[]
        {
            CanBeGuess,ZhixiangJiGeLang,UseSpeTask,TaskShuLiang,NeCo,
        }
    )
    {
        //ConfigurationHolder!.Illustration = NebulaAPI.AddonAsset.GetResource("BigPic/RefereePic.png")?.AsImage(115f);
    }
    // Virial.Media.Image? DefinedAssignable.IconImage => NebulaAPI.AddonAsset.GetResource("Smallicon/RefereeIcon.png")?.AsImage();

    public Citation Citation => Citations.TheOtherRoles;

    public static readonly Snitch MyRole = new Snitch();
    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => false;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield break;
    }
    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        string canguess = CanBeGuess ? Language.Translate("role.snitch.canBeGuess.true"): Language.Translate("role.snitch.canBeGuess.false");

        yield return new AssignableDocumentReplacement("%CanBeGuessed%", canguess);


        string imps;
        int impss = ZhixiangJiGeLang.GetValue();
        if (impss == 0)
            imps = Language.Translate("role.snitch.imposter.all");
        else
            imps = Language.Translate("role.snitch.imposter.count").Replace("%COUNT%", impss.ToString());

        string neutraltext;
        int neutrals = NeCo.GetValue();
        if (neutrals == 0)
            neutraltext = Language.Translate("role.snitch.neutral.any");
        else if (neutrals == 1)
            neutraltext = Language.Translate("role.snitch.neutral.badOnly");
        else
            neutraltext = Language.Translate("role.snitch.neutral.none");
        yield return new AssignableDocumentReplacement("%ImpInfo%", imps);
        yield return new AssignableDocumentReplacement("%NeuInfo%", neutraltext);
    }

    public RuntimeRole CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    static RemoteProcess<byte> RpcFlash = new("RpcFlash", (targetId, _) => {
        if (GamePlayer.GetPlayer(targetId)?.AmOwner == true)
            AmongUsUtil.PlayQuickFlash(UnityEngine.Color.green);
    });

    static RemoteProcess<(byte SnitchId, byte targetId)> RpcCreateArr = new("SniCreateArr", (msg, _) => {
        var sni = GamePlayer.GetPlayer(msg.SnitchId);
        if (sni?.Role is Snitch.Instance inst)
        {
            var target = GamePlayer.GetPlayer(msg.targetId);
            if (target != null)
            {
                var arrow = new TrackingArrowAbility(sni, 0f, new UnityEngine.Color(0, 1, 0), false);
                arrow.Register(inst);
                inst.Actarr.Add(arrow);
                if (!inst.arrmap.ContainsKey(sni.PlayerId))
                    inst.arrmap[sni.PlayerId] = new List<TrackingArrowAbility>();
                inst.arrmap[sni.PlayerId].Add(arrow);
            }
        }
    });
    public class Instance: RuntimeAssignableTemplate, RuntimeRole, RuntimeAssignable, IGameOperator
    {
        public DefinedRole Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        int lastTaskLeft = -1;
        internal List<TrackingArrowAbility> Actarr= new List<TrackingArrowAbility>();
        internal Dictionary<byte, List<TrackingArrowAbility>> arrmap = new();
        void Warning()
        {
            var AllKillers = GamePlayer.AllPlayers.Where(p => p.Role.Role.IsKiller && !p.IsDead).ToList();
            int i = NeCo.GetValue();
            var targets = AllKillers.Where(p =>
            {
                if (p.IsImpostor) return true;
                if (i == 0) return true;
                if (i == 1) return true;
                return false;
            }).ToList();
            AmongUsUtil.PlayQuickFlash(UnityEngine.Color.green);
            foreach (var target in targets)
            {
                RpcFlash.Invoke(target.PlayerId);
            }
            foreach (var target in targets)
            {
                RpcCreateArr.Invoke((MyPlayer.PlayerId, target.PlayerId));
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            
        }

        [Local]
        void AddTask(PlayerTasksTrySetLocalEvent ev)
        {
            if (!AmOwner) return;
            if (UseSpeTask)
            {
                int target = TaskShuLiang;
                int current = ev.Tasks.Count;
                if (target < current)
                {
                    ev.Tasks.RemoveRange(target, current - target);
                }
                else if (target > current)
                {
                    ev.AddExtraQuota(target - current);
                }
            }
        }
        [Local]
        void TaskCompleted(PlayerTaskCompleteLocalEvent ev)
        {
            if (!AmOwner) return;
            int Left = MyPlayer.Tasks.CurrentTasks;
            if (Left == 0 && lastTaskLeft != 0)
            {
                var imp=GamePlayer.AllPlayers.Where(p => p.IsImpostor && !p.IsDead).ToList();
                int arrs=ZhixiangJiGeLang.GetValue();
                if(arrs == 0) arrs = imp.Count;
                else arrs = Math.Min(arrs, imp.Count);
                var targets = imp.OrderBy(_ => Guid.NewGuid()).Take(arrs).ToList();
                foreach (var target in targets) 
                {
                    var arr = new TrackingArrowAbility(target, 0f, new UnityEngine.Color(0,1,0), false);
                    arr.Register(this);
                    Actarr.Add(arr);
                    if (!arrmap.ContainsKey(target.PlayerId))
                        arrmap[target.PlayerId] = new List<TrackingArrowAbility>();
                    arrmap[target.PlayerId].Add(arr);
                }
            }
            else if(Left==1 && lastTaskLeft != 1)
            {
                Warning();
            }
            lastTaskLeft = Left;
        }
        [Local]
        void Guessed(PlayerCanGuessPlayerLocalEvent ev)
        {
            if (!AmOwner) return;
            if (!CanBeGuess)
            {
                ev.CanGuess = false;
            }
        }
        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            foreach (var arrow in Actarr)
                arrow.Release();
            Actarr.Clear();
            arrmap.Clear();
        }
        [Local]
        void OnPlayerDie(PlayerDieEvent ev)
        {
            if (arrmap.TryGetValue(ev.Player.PlayerId, out var arrows))
            {
                foreach (var arrow in arrows) arrow.Release();
                arrmap.Remove(ev.Player.PlayerId);
            }
        }
    }
    
}