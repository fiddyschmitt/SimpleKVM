namespace SimpleKVM.Configuration
{
    public class AppSettings
    {
        public bool ForceInputChange { get; set; }

        //Default-on. Newtonsoft keeps this initializer when an older settings.json lacks the field.
        public bool FollowSourceChanges { get; set; } = true;
    }
}
