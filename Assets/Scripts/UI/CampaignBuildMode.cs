namespace DogLife
{
    // BuildPlayer.extraScriptingDefines selects the campaign package without changing editor playtests.
    public static class CampaignBuildMode
    {
        public static bool ExhibitionEnabled
        {
            get
            {
#if DOGLIFE_CAMPAIGN_BUILD
                return false;
#else
                return true;
#endif
            }
        }
    }
}
