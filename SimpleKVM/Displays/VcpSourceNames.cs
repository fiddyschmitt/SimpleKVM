namespace SimpleKVM.Displays
{
    public static class VcpSourceNames
    {
        public static string SourceIdToName(int sourceId)
        {
            //https://en.wikipedia.org/wiki/Monitor_Control_Command_Set
            //https://milek7.pl/ddcbacklight/mccs.pdf

            string sourceName = sourceId switch
            {
                -1 => "Leave unchanged",
                1 => "VGA 1",
                2 => "VGA 2",
                3 => "DVI 1",
                4 => "DVI 2",
                5 => "Composite video 1",
                6 => "Composite video 2",
                7 => "S-Video 1",
                8 => "S-Video 2",
                9 => "Tuner 1",
                10 => "Tuner 2",
                11 => "Tuner 3",
                12 => "Component video (YPrPb/YCrCb) 1",
                13 => "Component video (YPrPb/YCrCb) 2",
                14 => "Component video (YPrPb/YCrCb) 3",
                15 => "DisplayPort 1",
                16 => "DisplayPort 2",
                17 => "HDMI 1",
                18 => "HDMI 2",
                _ => $"{sourceId}",
            };

            return sourceName;
        }
    }
}
