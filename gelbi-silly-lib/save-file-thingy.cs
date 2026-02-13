using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace gelbi_silly_lib.SavedDataManagerExtensions;

internal class SavedDataManagerOI(SavedDataManager self, Action<Dictionary<string, object>> load, Action save)
{
  public static ConditionalWeakTable<OptionInterface, SavedDataManagerOI> managedInterfaces = new();

  public SavedDataManager instance = self;
  public Action<Dictionary<string, object>> load = load;
  public Action save = save;
}

public static class Extensions
{
  /// <summary>
  /// Binds option interface to this saved data manager
  /// </summary>
  /// <param name="self"></param>
  /// <param name="oi"><c>OptionInterface</c> instance to bind</param>
  /// <param name="load">Delegate invoked, when <c>OptionInterface</c> loads its config. Provides respective read data as an argument</param>
  /// <param name="save">Delegate invoked, when <c>OptionInterface</c> saves its config</param>
  public static void BindOptionInterface(this SavedDataManager self, OptionInterface oi, Action<Dictionary<string, object>> load, Action save)
  {
    if (!SavedDataManagerOI.managedInterfaces.TryGetValue(oi, out _))
      SavedDataManagerOI.managedInterfaces.Add(oi, new(self, load, save));
  }
}

/// <summary>
/// Class made to simplify binding saved data manager to option interface
/// </summary>
public abstract class BaseOIBinder<T1, T2> where T1: BaseSavedDataHandler where T2 : OptionInterface
{
  /// <summary>
  /// Underlying saved data manager
  /// </summary>
  public T1 manager;
  /// <summary>
  /// Bound option interface
  /// </summary>
  public T2 oi;

  /// <summary>
  /// Managed data
  /// </summary>
  public Dictionary<string, object> Data => manager.data;

  /// <summary>
  /// Initializes new instance of <see cref="BaseOIBinder{T1, T2}"/> class. Binds provided saved data manager to option interface
  /// </summary>
  public BaseOIBinder(T1 manager, T2 oi)
  {
    this.manager = manager;
    this.oi = oi;
    manager.manager.BindOptionInterface(oi, RemixLoad, RemixSave);
  }

  /// <summary>
  /// Reads saved data, updates stored one based on it and invokes <see cref="BaseSavedDataHandler.BaseLoad"/>
  /// </summary>
  public void BaseLoad(Dictionary<string, object> data)
  {
    manager.data = data;
    manager.BaseLoad();
  }

  /// <summary>
  /// Writes managed data to assigned save file
  /// </summary>
  public void Write() => manager.manager.Write(manager.data);

  /// <summary>
  /// Method invoked when remix menu loads data for bound option interface
  /// </summary>
  /// <param name="data">Data, loaded from assigned save file</param>
  public abstract void RemixLoad(Dictionary<string, object> data);

  /// <summary>
  /// Method invoked when remix menu saves data for bound option interface
  /// </summary>
  public abstract void RemixSave();
}