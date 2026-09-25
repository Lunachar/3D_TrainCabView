using System;

namespace SortingStation
{
    [Serializable]
    public sealed class UserPreferences
    {
        public int preferencesVersion = 5;
        public float masterVolume = 1f;
        public float musicVolume = 0.55f;
        public float effectsVolume = 0.85f;
        public float speechVolume = 1f;
        public bool speechEnabled = true;
        public MotionLevel motionLevel = MotionLevel.Normal;
        public float inputCooldown = 0.18f;
        public SeasonMode seasonMode = SeasonMode.Auto;
        public bool routePromptsEnabled = true;
        public bool gentleInteractionsEnabled = true;
        public bool gentleHintsEnabled = true;
        public CabWorldMode cabWorldMode = CabWorldMode.Immersive3D;
        public CabWorldQuality cabWorldQuality = CabWorldQuality.Balanced;

        public void Upgrade()
        {
            if (preferencesVersion < 2)
            {
                seasonMode = SeasonMode.Auto;
                routePromptsEnabled = true;
                preferencesVersion = 2;
            }
            if (preferencesVersion < 3)
            {
                gentleInteractionsEnabled = true;
                gentleHintsEnabled = true;
                preferencesVersion = 3;
            }
            if (preferencesVersion < 4)
            {
                cabWorldMode = CabWorldMode.Hybrid3D;
                cabWorldQuality = CabWorldQuality.Balanced;
                preferencesVersion = 4;
            }
            if (preferencesVersion < 5)
            {
                cabWorldMode = CabWorldMode.Immersive3D;
                preferencesVersion = 5;
            }
            if (!System.Enum.IsDefined(typeof(SeasonMode), seasonMode)) seasonMode = SeasonMode.Auto;
            if (!System.Enum.IsDefined(typeof(CabWorldMode), cabWorldMode)) cabWorldMode = CabWorldMode.Immersive3D;
            if (!System.Enum.IsDefined(typeof(CabWorldQuality), cabWorldQuality)) cabWorldQuality = CabWorldQuality.Balanced;
        }

        public UserPreferences Clone()
        {
            return (UserPreferences)MemberwiseClone();
        }
    }
}
