using System.ComponentModel;

namespace EnvDTE.Helpers
{
    public enum TargetFramework
    {
        [Description("3.5")]
        v3_5,
        [Description("4.0")]
        v4_0,
        [Description("4.5")]
        v4_5,
        [Description("4.5.1")]
        v4_5_1,
        [Description("4.5.2")]
        v4_5_2,
        [Description("4.6")]
        v4_6
    }
}
