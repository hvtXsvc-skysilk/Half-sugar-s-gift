using Citations = hvtXsvc.Core.Citations;
using NPlayer = Virial.Game.Player;

  namespace NebulaN.Scripts.Roles.Impostor
{
    public class BeNothing : DefinedRoleTemplate, HasCitation, DefinedRole, RuntimeAssignableGenerator<RuntimeRole>
    {
        static private FloatConfiguration cooldown = NebulaAPI.Configurations.Configuration(
        "options.role.BeNothing.cooldown",          
        (0f, 60f, 2.5f),                            
        10f,                                         
        FloatConfigurationDecorator.Second,          
        null, null
         );

        static private FloatConfiguration duration = NebulaAPI.Configurations.Configuration(
        "options.role.BeNothing.duration",
        (0.5f, 20f, 0.5f),
        10f,
        FloatConfigurationDecorator.Second,
        null, null
         );

        static private FloatConfiguration speedMultiplier = NebulaAPI.Configurations.Configuration(
        "options.role.BeNothing.speedMultiplier",
        (1.0f, 3.0f, 0.25f),
        1.5f,
        FloatConfigurationDecorator.None,            
        null, null
         );
        private BeNothing() : base(
            "BeNothing",
            new Virial.Color(Palette.ImpostorRed.r, Palette.ImpostorRed.g, Palette.ImpostorRed.b),
            RoleCategory.ImpostorRole,
            NebulaTeams.ImpostorTeam,
            new Virial.Configuration.IConfiguration[] { cooldown, duration, speedMultiplier })
        {
            
        }


        public Citation Citation => Citations.hvtXsvc_hsg;

        public RuntimeRole CreateInstance(NPlayer player, int[] arguments)
        {
            return new Instance(player);

        }
        AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;
        public static readonly BeNothing MyRole = new BeNothing();
        public class Instance : RuntimeAssignableTemplate, RuntimeRole
        {
            
            public Instance(NPlayer player) : base(player) { }

            
            public DefinedRole Role => MyRole;
            public void OnActivated()
            {
                if (AmOwner)
                {
                    float dur = duration;
                    float spd = speedMultiplier; 
                    var BNIcon = NebulaAPI.AddonAsset.GetResource("BeNothingButton.png")?.AsImage(100f);

                   
                   
                    var btn = NebulaAPI.Modules.AbilityButton(
                        this, MyPlayer,
                        VirtualKeyInput.Ability,
                        cooldown,                
                        "BeNothing",
                        BNIcon,
                        (ModAbilityButton _) => true,
                        (button) => !MyPlayer.IsDead,
                        false
                    );

                    
                    btn.EffectTimer = NebulaAPI.Modules.Timer(this, duration);   

                    
                    btn.OnEffectEnd = (button) =>
                    {
                        button.StartCoolDown();
                    };

                    
                    btn.OnClick = (button) =>
                    {
                        if (button.IsInEffect) return;
                        float dur = duration;     
                        float spd = speedMultiplier;

                        MyPlayer.GainAttribute(PlayerAttributes.Invisible, dur, false, 100);
                        MyPlayer.GainSpeedAttribute(spd, dur, false, 100);

                        button.StartEffect();    
                    };
                }
            }



            public bool CanUseVent => false;
        }
    }
}