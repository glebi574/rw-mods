using gelbi_silly_lib.Converter;

namespace gelbi_silly_lib;

public class GSLSettingsManager : BaseSavedDataHandler
{
  public bool wrapHooks = false, noUpdateDisable = false, disableEOS = false, biggerErrorBox = true;

  public GSLSettingsManager(string filename) : base(filename) { }

  public GSLSettingsManager(string[] nestedFolders, string filename) : base(nestedFolders, filename) { }

  public override void BaseLoad()
  {
    data.TryUpdateValueWithType("wrapHooks", ref wrapHooks);
    data.TryUpdateValueWithType("noUpdateDisable", ref noUpdateDisable);
    data.TryUpdateValueWithType("disableEOS", ref disableEOS);
    data.TryUpdateValueWithType("biggerErrorBox", ref biggerErrorBox);
  }
}

public static class GSLSettings
{
  public static GSLSettingsManager instance;

  static GSLSettings()
  {
    instance = new(["gelbi"], "gsl");
  }
}