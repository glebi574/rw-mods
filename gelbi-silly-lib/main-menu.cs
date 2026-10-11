using BepInEx;
using gelbi_silly_lib.Converter;
using gelbi_silly_lib.Other;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace gelbi_silly_lib;

public static class MainMenuHooks
{
  public static void Apply()
  {
    On.Menu.MainMenu.ctor += MainMenu_ctor;
    On.Menu.MainMenu.Update += MainMenu_Update;
    On.Menu.MainMenu.Singal += MainMenu_Singal;
    On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;
    IL.Menu.MainMenu.AddMainMenuButton += MainMenu_AddMainMenuButton;
  }

  public static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
  {
    if (self.pendingProcess == null && ID == ChangelogMenu.id)
      self.currentMainLoop = new ChangelogMenu(self);
    orig(self, ID);
  }

  public static NotificationWindow notificationWindow;
  public static SimpleButton changelogButton;
  public static FSprite changelogIcon;
  public static int changelogButtonColorModifier = 0;
  public static bool parsedNotifications = false;
  public static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
  {
    orig(self, manager, showRegionSpecificBkg);
    if (!parsedNotifications)
    {
      parsedNotifications = true;
      if (IssueResolver.invalidWorkshopItems.Count != 0)
      {
        List<PublishedFileId_t> invalidMods = [];
        foreach (PublishedFileId_t id in IssueResolver.invalidWorkshopItems)
          if (!SteamUGC.GetItemInstallInfo(id, out _, out _, 1024, out _))
            invalidMods.Add(id);
        Notification.AddTemporaryNotification(typeof(Plugin), "Broken workshop items", "Some of workshop items you're subscribed to are broken. GSL prevents them causing infninite loading screen while enabled. Press 'Yes' to unsubscribe from these, or, alternatively unsubscribe from them manually (they're blank in subscribed items tab with their workshop pages being inaccessible).".SplitByLength(68), () =>
        {
          try
          {
            foreach (PublishedFileId_t id in invalidMods)
              SteamUGC.UnsubscribeItem(id);
          }
          catch (Exception e)
          {
            LogError("Failed to unsubscribe from broken workshop items: " + e);
          }
        }, () => { });
        IssueResolver.invalidWorkshopItems.Clear();
      }

      foreach (string path in FileUtils.ResolveAllFilePaths("notifications.json", true))
      {
        string mod = Directory.GetParent(path).Name;
        try
        {
          object data = Json.JsonParser.Parse(File.ReadAllText(path));
          if (data is not List<object> notifications)
            throw new Exception("Expected array with notifications");
          foreach (object notification in notifications)
          {
            if (notification is not Dictionary<string, object> notificationData)
              throw new Exception("Expected dictionary with notification data");
            if (!notificationData.TryGetValueWithType("title", out string title))
              throw new Exception("Missing required field with string value 'title'");
            if (!notificationData.TryGetValueWithType("message", out string message))
              throw new Exception("Missing required field with string value 'message'");
            object additionalData = null;
            if (notificationData.TryGetNumber("chance", out float chance))
              additionalData = chance;
            else if (notificationData.TryGetValueWithType("updateTarget", out string updateTarget))
              additionalData = updateTarget;
            Notification.AddNotificationImpl(ModUtils.mods[mod].name, mod, title,
              notificationData.TryGetValueWithType("autosplit", out bool autosplit) && autosplit ? message.SplitByLength(68) : message, additionalData, null, null);
          }
        }
        catch (Exception e)
        {
          LogError($"Failed to read notifications from {mod}\\notifications.json: {e}");
        }
      }
      if (NotificationWindow.current < Notification.pending.Count)
        notificationWindow = new NotificationWindow(self);
    }
    if (Plugin.pluginInterface.hideChangelogs.Value)
      return;

    self.AddMainMenuButton(changelogButton = new(self, self.pages[0], "CHANGELOGS", "_CHANGELOGS", new(0f, 30f), new(MainMenu.GetButtonWidth(self.CurrLang), 30f)), () =>
    {
      self.manager.RequestMainProcessSwitch(ChangelogMenu.id);
      self.PlaySound(SoundID.MENU_Switch_Page_In);
    }, 1);
  }

  public static void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, MainMenu self)
  {
    orig(self);
    if (!ChangelogMenu.modsUpdated || Plugin.pluginInterface.hideChangelogs.Value)
      return;
    float modifier = 1f - Mathf.Pow(Mathf.Sin(++changelogButtonColorModifier / 30f), 2);
    Color color = new(0.664f + modifier * 0.3f, 0.644f + modifier * 0.2f, 0.696f - modifier * 0.2f);
    changelogButton.rectColor = changelogButton.labelColor = JollyCoop.JollyCustom.RGB2HSL(color);
    if (changelogButton.menuLabel.myText.Length == 0 && changelogIcon != null)
      changelogIcon.color = color;
  }

  public static void MainMenu_AddMainMenuButton(ILContext il)
  {
    ILCursor c = new(il);

    if (c.TryGotoNext(i => i.MatchStloc(1)))
      c.Emit(OpCodes.Pop).Emit(OpCodes.Ldc_I4, 11);
    c.Index = il.Instrs.Count - 1;
    c.Emit(OpCodes.Ldarg_0).Emit(OpCodes.Ldarg_1).Emit(OpCodes.Call, ((Delegate)SillyMainMenu.AddMainMenuButton).Method);
  }

  public static void MainMenu_Singal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
  {
    orig(self, sender, message);
    if (!message.StartsWith("_GSLNOTIFICATION"))
      return;
    self.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
    Notification notification = Notification.pendingSequence[NotificationWindow.current];
    (message[message.Length - 1] == 'S' ? notification.handlerYes : notification.handlerNo)?.Invoke();
    ++NotificationWindow.current;
    notificationWindow.UpdateNotification();
    if (notification.isTemporary)
      return;
    if (!Notification.saveData.data.TryGetValueWithType("activated", out List<object> savedActivated))
      Notification.saveData.data["activated"] = savedActivated = [];
    if (Notification.saveData.data.TryGetValueWithType("pending", out List<object> savedPending))
      for (int i = 0; i < savedPending.Count; ++i)
        if (savedPending[i] is Dictionary<string, object> data
         && data.TryGetValueWithType("source", out string source) && source == notification.source
         && data.TryGetValueWithType("title", out string title) && title == notification.title)
        {
          savedPending.RemoveAt(i);
          break;
        }
    savedActivated.Add($"{notification.source}@{notification.title}");
    Notification.saveData.Write();
  }
}

public class SillyMainMenu
{
  public static ConditionalWeakTable<MainMenu, SillyMainMenu> sillyMenus = new();
  /// <summary>
  /// Information, allowing main menu button with given signal to be overridden.
  /// <para><c>spriteDef</c> instantiates and returns sprite used for the button</para>
  /// <para><c>specialPos</c> returns required relative position of the button.
  /// If <c>null</c> or selected position is taken, position will be selected automatically. Set y value below <c>0</c> or expect issues.</para>
  /// </summary>
  public static Dictionary<string, (Func<FSprite> spriteDef, Func<SimpleButton, IntVector2> specialPos)> supportedButtons = new() {
    { "STORY", (() => new("Kill_Slugcat"), button => new(-1, 0)) },
    { "ARENA", (() => new("Symbol_FireSpear") { color = new(0.92f, 0.06f, 0.06f) }, button => new(0, 0)) },
    { "EXPEDITION", (() => new("DragonSlayerB") { shader = Custom.rainWorld.Shaders["MenuText"] }, button => new(1, 0)) },
    { "REGIONS", (() => new("Symbol_FlyLure") { color = new(0.75f, 0.25f, 0.2f) }, button => new(-1, 1)) },
    { "COLLECTION", (() => new("Symbol_Pearl") { color = new(0.92f, 0.94f, 0.33f) }, button => new(0, 1)) },
    { "REMIX", (() => new("enableAll"), button => new(-1, 2)) },
    { "OPTIONS", (() => new("Kill_Garbageworm") { color = new(0.68f, 0.68f, 0.68f) }, button => new(0, 2)) },
    { "EXIT", (() => new("ShortcutShelter"), button => new(1, 2)) },

    { "_CHANGELOGS", (() => MainMenuHooks.changelogIcon = new("Menu_Symbol_Show_List"), button => new(0, -1)) },
    { "GETFANCY", (() => new("GuidanceSlugcat") { color = new(0.92f, 0.5f, 0.44f) }, button => new(-1, -1)) },
    { "MEADOW", (() => new("ShortcutGate") { color = new(1f, 0.6f, 0.8f) }, button => new(0, -2)) },
    { "SAVES", (() => new("Menu_Symbol_Show_Thumbs") { color = new(0.6f, 0.6f, 0.8f) }, button => new(1, -1)) },
  };
  public static IntVector2 baseSize = new(3, 3);
  public static float buttonSize = 30f, buttonSpace = 10f, buttonTuple = buttonSize + buttonSpace, halfButton = buttonSize / 2f;

  public ConditionalWeakTable<SimpleButton, FSprite> managedSprites = new();
  public MainMenu self;
  public RoundedRect other = null;
  public float baseX, baseY;

  public static void AddMainMenuButton(MainMenu self, SimpleButton button)
  {
    if (Plugin.pluginInterface.sillyMainMenu.Value)
      sillyMenus.GetValue(self, e => new(self)).Update(button);
  }

  public SillyMainMenu(MainMenu self)
  {
    this.self = self;
    baseX = self.mainMenuButtons[0].pos.x + self.mainMenuButtons[0].size.x / 2f;
    baseY = GSLUtils.ScreenHeight / 2f - 30f + buttonTuple;
    float sizeX = baseSize.x * buttonTuple + buttonSpace, sizeY = baseSize.y * buttonTuple + buttonSpace;
    self.pages[0].subObjects.Add(new RoundedRect(self, self.pages[0], new(baseX - sizeX / 2f, baseY - sizeY + halfButton + buttonSpace), new(sizeX, sizeY), false));
  }

  public void UpdateButton(SimpleButton button, int x, int y)
  {
    button.pos.x = baseX + x * buttonTuple - halfButton;
    button.pos.y = baseY + y * buttonTuple - halfButton;
    if (!managedSprites.TryGetValue(button, out FSprite sprite))
      return;
    sprite.x = button.pos.x + halfButton + 1f + GSLUtils.WidthCorrection;
    sprite.y = button.pos.y + halfButton;
  }

  public void Update(SimpleButton button)
  {
    if (supportedButtons.TryGetValue(button.signalText, out (Func<FSprite> spriteDef, Func<SimpleButton, IntVector2> specialPos) replacement))
    {
      button.SetSize(new(buttonSize, buttonSize));
      button.menuLabel.text = "";
      FSprite sprite = replacement.spriteDef();
      managedSprites.Add(button, sprite);
      button.Container.AddChild(sprite);
    }
    List<(IntVector2 pos, SimpleButton button)> placedButtons = [];
    List<SimpleButton> assignedButtons = [], unsupportedButtons = [];
    foreach (SimpleButton b in self.mainMenuButtons)
    {
      if (!supportedButtons.TryGetValue(b.signalText, out replacement))
      {
        unsupportedButtons.Add(b);
        continue;
      }
      if (replacement.specialPos == null)
      {
        assignedButtons.Add(b);
        continue;
      }
      IntVector2 pos = replacement.specialPos(b);
      if (pos.y < 0)
      {
        placedButtons.Add((pos, b));
        continue;
      }
      UpdateButton(b, pos.x, -pos.y);
    }
    int amount = placedButtons.Count + assignedButtons.Count, x = 0, y = 0;
    float offset = baseSize.x * buttonTuple + buttonSpace;
    if (amount != 0)
    {
      if (amount < 4)
      {
        x = 3;
        y = 1;
      }
      else if (amount < 7)
      {
        x = 3;
        y = 2;
      }
      else if (amount < 10)
      {
        x = 3;
        y = 3;
      }
      else if (amount < 16)
      {
        x = 5;
        y = 3;
      }
      else if (amount < 21)
      {
        x = 5;
        y = 4;
      }
      else if (amount < 26)
      {
        x = 5;
        y = 5;
      }
      else
      {
        x = amount / 5;
        if (amount % 5 != 0)
          ++x;
        if ((x & 1) == 0)
          ++x;
        y = 5;
      }
      offset = (x / 2 + 1) * buttonTuple + buttonSpace;
      other?.RemoveSprites();
      float sizeX = x * buttonTuple + buttonSpace, sizeY = y * buttonTuple + buttonSpace;
      self.pages[0].subObjects.Add(other = new(self, self.pages[0], new(baseX - sizeX / 2f, baseY - sizeY + halfButton + buttonSpace - buttonTuple * 4), new(sizeX, sizeY), false));
      HashSet<(int x, int y)> placed = [];
      foreach ((IntVector2 pos, SimpleButton b) in placedButtons)
        if (pos.x >= x / -2 && pos.x <= x / 2 && pos.y >= -y && pos.y <= 0 && placed.Add((pos.x, pos.y)))
          UpdateButton(b, pos.x, pos.y - 3);
        else
          assignedButtons.Add(b);
      int i = 0, lx, ly;
      foreach (SimpleButton b in assignedButtons)
      {
        while (placed.Contains((lx = i % x - x / 2, ly = i++ / -x - 1))) ;
        UpdateButton(b, lx, ly - 3);
      }
    }
    for (int i = 0; i < unsupportedButtons.Count; ++i)
      unsupportedButtons[i].pos = new(baseX + offset, baseY - i * buttonTuple - halfButton);
    Dictionary<IntVector2, SimpleButton> grid = [];
    SimpleButton rightButton = unsupportedButtons.Count > 0 ? unsupportedButtons[0] : null;
    foreach (SimpleButton b in self.mainMenuButtons)
      b.nextSelectable = new MenuObject[4];
    foreach (SimpleButton b in assignedButtons)
      grid[new((int)(b.pos.x / buttonTuple), (int)(b.pos.y / buttonTuple))] = b;
    foreach ((_, SimpleButton b) in placedButtons)
      grid[new((int)(b.pos.x / buttonTuple), (int)(b.pos.y / buttonTuple))] = b;
    for (int i = 0; i < unsupportedButtons.Count; ++i)
    {
      SimpleButton b = unsupportedButtons[i];
      b.nextSelectable[0] = self.mainMenuButtons[0];
      b.nextSelectable[2] = self.mainMenuButtons[1];
      b.nextSelectable[1] = unsupportedButtons[(i + i - 1) % unsupportedButtons.Count];
      b.nextSelectable[3] = unsupportedButtons[(i + 1) % unsupportedButtons.Count];
    }
    foreach (KeyValuePair<IntVector2, SimpleButton> cell in grid)
    {
      for (int cx = -1; cx >= x / -2; --cx)
        if (grid.TryGetValue(cell.Key + new IntVector2(cx, 0), out SimpleButton neighbour))
        {
          cell.Value.nextSelectable[0] = neighbour;
          goto _c0;
        }
      cell.Value.nextSelectable[0] = cell.Value;
    _c0:
      for (int cy = -1; cy >= y / -2; --cy)
        if (grid.TryGetValue(cell.Key + new IntVector2(0, cy), out SimpleButton neighbour))
        {
          cell.Value.nextSelectable[1] = neighbour;
          goto _c1;
        }
      cell.Value.nextSelectable[1] = cell.Value;
    _c1:
      for (int cx = 1; cx <= x / 2; ++cx)
        if (grid.TryGetValue(cell.Key + new IntVector2(cx, 0), out SimpleButton neighbour))
        {
          cell.Value.nextSelectable[2] = neighbour;
          goto _c2;
        }
      cell.Value.nextSelectable[2] = cell.Value;
    _c2:
      for (int cy = 1; cy <= y / 2; ++cy)
        if (grid.TryGetValue(cell.Key + new IntVector2(0, cy), out SimpleButton neighbour))
        {
          cell.Value.nextSelectable[3] = neighbour;
          goto _c3;
        }
      cell.Value.nextSelectable[3] = cell.Value;
    _c3:
      for (int i = 0; i < 4; ++i)
        if (cell.Value.nextSelectable[i] != cell.Value)
          goto _ce;
      for (int i = 0; i < 4; ++i)
        cell.Value.nextSelectable[i] = self.mainMenuButtons[5];
      _ce:;
    }
  }
}

public class NotificationSavedData : BaseSavedDataHandler
{
  public NotificationSavedData(string filename) : base(filename) { }

  public NotificationSavedData(string[] nestedFolders, string filename) : base(nestedFolders, filename) { }

  public override void BaseLoad()
  {
    data.TryGetValueWithType("pending", out List<object> pending);
    data.TryGetValueWithType("activated", out List<object> activated);

    foreach (object obj in pending ?? [])
      if (obj is Dictionary<string, object> notification
        && notification.TryGetValueWithType("source", out string source)
        && notification.TryGetValueWithType("title", out string title)
        && notification.TryGetValueWithType("message", out string message))
        Notification.pendingSequence.Add(Notification.pending[$"{source}@{title}"] = new(source, title, message));
      else
        GSLLog.GLog("[Warning] Failed to parse one of pending notifications");
    foreach (object obj in activated ?? [])
      if (obj is string id)
        Notification.activated.Add(id);
      else
        GSLLog.GLog("[Warning] Failed to parse one of activated notifications");
  }
}

/// <summary>
/// Allows to display notifications in the main menu
/// </summary>
public class Notification
{
  public static Dictionary<string, Notification> pending = [];
  public static List<Notification> pendingSequence = [];
  public static HashSet<string> activated = [];
  public static NotificationSavedData saveData = new(["gelbi"], "sent-notifications");

  public string source, title, message;
  public bool isTemporary;
  public Action handlerYes, handlerNo;

  internal Notification(string source, string title, string message, bool isTemporary = false)
  {
    this.source = source;
    this.title = title;
    this.message = message;
    this.isTemporary = isTemporary;
  }

  internal static void AddNotificationImpl(string sourceName, string modFolder, string title, string message, object data, Action handlerYes, Action handlerNo)
  {
    string id = $"{sourceName}@{title}";
    if (pending.TryGetValue(id, out Notification notification))
    {
      notification.handlerYes ??= handlerYes;
      notification.handlerNo ??= handlerNo;
      return;
    }
    if (activated.Contains(id))
      return;
    if (!saveData.data.TryGetValueWithType("pending", out List<object> savedPending))
      saveData.data["pending"] = savedPending = [];
    savedPending.Add(new Dictionary<string, object>() { { "source", sourceName }, { "title", title }, { "message", message } });
    saveData.Write();
    if (data is float chance)
    {
      if (RXRandom.Float() <= chance)
        pendingSequence.Add(pending[id] = new(sourceName, title, message) { handlerYes = handlerYes, handlerNo = handlerNo });
      return;
    }
    if (data is string updateTarget)
    {
      if (ModUtils.previousModVersions.data.TryGetValueWithType(modFolder, out string version) && version != updateTarget && ModUtils.mods.TryGetValue(modFolder, out ModManager.Mod mod) && mod.version == version)
        pendingSequence.Add(pending[id] = new(sourceName, title, message) { handlerYes = handlerYes, handlerNo = handlerNo });
      return;
    }
    if (data is null)
      pendingSequence.Add(pending[id] = new(sourceName, title, message) { handlerYes = handlerYes, handlerNo = handlerNo });
  }

  static void AddNotificationFromType(Type source, string title, string message, object data, Action handlerYes, Action handlerNo)
  {
    object[] attributes = source.GetCustomAttributes(typeof(BepInPlugin), false);
    AddNotificationImpl(attributes.Length == 0 ? "\r" : ((BepInPlugin)attributes[0]).Name, source.Assembly.GetModFolderName(), title, message, data, handlerYes, handlerNo);
  }

  /// <summary>
  /// Queues notification to be displayed
  /// </summary>
  /// <param name="source">Sender type with <see cref="BepInPlugin"/> metadata</param>
  /// <param name="title">Displayed title. Must be unique among other notifications sent by your mod</param>
  /// <param name="message">Displayed mesage</param>
  /// <param name="handlerYes">Delegate invoked when user presses OK or YES</param>
  /// <param name="handlerNo">Delegate invoked when user presses NO. If not <c>null</c>, notification will have YES and NO buttons instead of just OK</param>
  public static void AddNotification(Type source, string title, string message, Action handlerYes = null, Action handlerNo = null)
    => AddNotificationFromType(source, title, message, null, handlerYes, handlerNo);
  /// <summary>
  /// Queues notification to be displayed if previous recorded modinfo version of the mod doesn't match current one and current version matches <paramref name="updateTarget"/>
  /// </summary>
  /// <param name="source">Sender type with <see cref="BepInPlugin"/> metadata</param>
  /// <param name="title">Displayed title. Must be unique among other notifications sent by your mod</param>
  /// <param name="message">Displayed mesage</param>
  /// <param name="updateTarget">Target modinfo version</param>
  /// <param name="handlerYes">Delegate invoked when user presses OK or YES</param>
  /// <param name="handlerNo">Delegate invoked when user presses NO. If not <c>null</c>, notification will have YES and NO buttons instead of just OK</param>
  public static void AddNotification(Type source, string title, string message, string updateTarget, Action handlerYes = null, Action handlerNo = null)
    => AddNotificationFromType(source, title, message, updateTarget, handlerYes, handlerNo);
  /// <summary>
  /// Queues notification to be displayed with certain chance, calculated once on startup
  /// </summary>
  /// <param name="source">Sender type with <see cref="BepInPlugin"/> metadata</param>
  /// <param name="title">Displayed title. Must be unique among other notifications sent by your mod</param>
  /// <param name="message">Displayed mesage</param>
  /// <param name="chance">Chance for notification to be displayed</param>
  /// <param name="handlerYes">Delegate invoked when user presses OK or YES</param>
  /// <param name="handlerNo">Delegate invoked when user presses NO. If not <c>null</c>, notification will have YES and NO buttons instead of just OK</param>
  public static void AddNotification(Type source, string title, string message, float chance, Action handlerYes = null, Action handlerNo = null)
    => AddNotificationFromType(source, title, message, chance, handlerYes, handlerNo);
  /// <summary>
  /// Queues notification to be displayed. This notification will not be preserved and doesn't have to be unique compared to other notifications sent by your mod
  /// </summary>
  /// <param name="source">Sender type with <see cref="BepInPlugin"/> metadata</param>
  /// <param name="title">Displayed title. Must be unique among other notifications sent by your mod</param>
  /// <param name="message">Displayed mesage</param>
  /// <param name="handlerYes">Delegate invoked when user presses OK or YES</param>
  /// <param name="handlerNo">Delegate invoked when user presses NO. If not <c>null</c>, notification will have YES and NO buttons instead of just OK</param>
  public static void AddTemporaryNotification(Type source, string title, string message, Action handlerYes = null, Action handlerNo = null)
  {
    object[] attributes = source.GetCustomAttributes(typeof(BepInPlugin), false);
    string sourceName = attributes.Length == 0 ? "\r" : ((BepInPlugin)attributes[0]).Name;
    pendingSequence.Add(pending[$"{sourceName}@{title}:{sourceName.GetHashCode()}"] = new(sourceName, title, message, true) { handlerYes = handlerYes, handlerNo = handlerNo });
  }
}

public class NotificationWindow
{
  public static float margin = 10f, width = 400f, height = 244f, buttonWidth = 110f, buttonHeight = 30f, anchorX, anchorY;
  public static int current = 0;

  public Page page;
  public MainMenu self;
  public RoundedRect window;
  public MenuLabel header, title, source, message;
  public SimpleButton buttonYes, buttonNo;
  public MenuContainer container;

  public void UpdateNotification()
  {
    if (current >= Notification.pendingSequence.Count)
    {
      container.RemoveSprites();
      return;
    }
    Notification notification = Notification.pendingSequence[current];
    header.text = $"NOTIFICATION {current + 1}/{Notification.pending.Count}";
    title.text = notification.title;
    source.text = $"Source: {notification.source}";
    message.text = notification.message;
    if (notification.handlerNo == null)
    {
      buttonYes.menuLabel.text = "OK";
      if (buttonNo == null)
        return;
      buttonNo.RemoveSprites();
      buttonNo = null;
    }
    else
    {
      buttonYes.menuLabel.text = "YES";
      if (buttonNo == null)
        page.subObjects.Add(buttonNo = new(self, page, "NO", "_GSLNOTIFICATIONNO", new(anchorX + buttonWidth / 2f + margin, anchorY - height + margin), new(buttonWidth, buttonHeight)));
    }
  }

  public NotificationWindow(MainMenu self)
  {
    anchorX = GSLUtils.ScreenWidth - width / 2f - margin - GSLUtils.WidthCorrection;
    anchorY = GSLUtils.ScreenHeight - margin;
    page = (this.self = self).pages[0];
    container = new(self, page, new(0f, 0f));
    page.subObjects.Add(container);
    container.subObjects.AddRange([
      window = new(self, container, new(anchorX - width / 2f, anchorY - height), new(width, height), true) { fillAlpha = 0.8f },
      header = new(self, container, "", new(anchorX, anchorY - 20f), new(0f, 0f), true),
      title = new(self, container, "", new(anchorX, anchorY - 50f), new(0f, 0f), true),
      source = new(self, container, "", new(anchorX - width / 2f + margin, anchorY - 70f), new(0f, 0f), false),
      message = new(self, container, "", new(anchorX - width / 2f + margin, anchorY - 80f), new(0f, 0f), false),
      buttonYes = new(self, container, "", "_GSLNOTIFICATIONYES", new(anchorX - buttonWidth / 2f, anchorY - height + margin), new(buttonWidth, buttonHeight))
    ]);
    window.Container.AddChild(new FSprite("pixel") { scaleX = width - margin * 4f, scaleY = 2f, x = anchorX + GSLUtils.WidthCorrection, y = anchorY - 36f });
    source.label.anchorX = message.label.anchorX = 0f;
    message.label.anchorY = 1f;
    UpdateNotification();
  }
}