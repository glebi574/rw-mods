using gelbi_silly_lib.Converter;
using gelbi_silly_lib.Other;
using Menu;
using Menu.Remix.MixedUI;
using RWCustom;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib;

public static class MainMenuHooks
{
  public static void Apply()
  {
    On.Menu.MainMenu.ctor += MainMenu_ctor;
    On.Menu.MainMenu.Singal += MainMenu_Singal;
    On.Menu.MainMenu.Update += MainMenu_Update;
    On.ProcessManager.PostSwitchMainProcess += ProcessManager_PostSwitchMainProcess;
  }

  static void ProcessManager_PostSwitchMainProcess(On.ProcessManager.orig_PostSwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
  {
    if (self.pendingProcess == null && ID == ChangelogMenu.id)
      self.currentMainLoop = new ChangelogMenu(self);
    orig(self, ID);
  }

  public static SimpleButton changelogButton;
  public static int changelogButtonColorModifier = 0;
  static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
  {
    orig(self, manager, showRegionSpecificBkg);

    changelogButton = new(self, self.pages[0], "CHANGELOGS", "_CHANGELOGS", new(Custom.rainWorld.options.ScreenSize.x / 2f - 50f, 30f), new(110f, 30f));
    self.pages[0].subObjects.Add(changelogButton);
    self.pages[0].selectables.Add(changelogButton);
  }

  static void MainMenu_Singal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
  {
    orig(self, sender, message);
    if (message == "_CHANGELOGS")
    {
      self.PlaySound(SoundID.MENU_Switch_Page_In);
      self.manager.RequestMainProcessSwitch(ChangelogMenu.id);
    }
  }

  static void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, MainMenu self)
  {
    orig(self);
    if (!ChangelogMenu.modsUpdated)
      return;
    float modifier = 1f - Mathf.Pow(Mathf.Sin(++changelogButtonColorModifier / 30f), 2);
    HSLColor color = JollyCoop.JollyCustom.RGB2HSL(new(0.664f + modifier * 0.3f, 0.644f + modifier * 0.2f, 0.696f - modifier * 0.2f));
    changelogButton.labelColor = color;
    changelogButton.rectColor = color;
  }
}

public class ChangelogData
{
  public bool updated;
  public string changelog, modLabel;
  public List<int> lineIndexes;

  public ChangelogData(ModManager.Mod mod, string changelog)
  {
    this.changelog = changelog;
    modLabel = $"{mod.name} {mod.version}";
    updated = !ModUtils.previousModVersions.data.TryGetValueWithType(Path.GetFileName(mod.path), out string version) || version != mod.version;
    lineIndexes = [0];
    for (int i = 1; i < changelog.Length - 1; ++i)
      if (changelog[i] == '\n')
        lineIndexes.Add(i);
  }
}

public class ChangelogMenu : Menu.Menu
{
  public static bool modsUpdated = false;
  public static ProcessManager.ProcessID id = new("ChangelogMenu", true);
  public static Dictionary<string, ChangelogData> activeChangelogs = [];

  public MenuLabel changelog, modName;
  public DyeableRect rectBack;
  public List<ChangelogData> changelogList = [];
  public ChangelogData selectedChangelog;
  public int changelogIndex = 0, viewLevel = 0, rowLimit;

  public ChangelogMenu(ProcessManager manager) : base(manager, id)
  {
    modsUpdated = false;
    ModManager.Mod currentMod;
    if (activeChangelogs.Count != 0)
      foreach (KeyValuePair<string, string> changelog in ModUtils.changelogs)
        if (!(currentMod = ModUtils.mods[changelog.Key]).enabled)
          activeChangelogs.Remove(currentMod.name);
    foreach (KeyValuePair<string, string> changelog in ModUtils.changelogs)
      if ((currentMod = ModUtils.mods[changelog.Key]).enabled && !activeChangelogs.ContainsKey(currentMod.name))
        activeChangelogs[currentMod.name] = new(currentMod, changelog.Value);

    foreach (KeyValuePair<string, ChangelogData> changelog in activeChangelogs)
      if (changelog.Value.updated)
        changelogList.Add(changelog.Value);
    foreach (KeyValuePair<string, ChangelogData> changelog in activeChangelogs)
      if (!changelog.Value.updated)
        changelogList.Add(changelog.Value);

    float screenX = Custom.rainWorld.options.ScreenSize.x, screenY = Custom.rainWorld.options.ScreenSize.y;
    Page mainPage = new(this, null, "main", 0);
    pages.Add(mainPage);
    mainPage.subObjects.Add(scene = new InteractiveMenuScene(this, mainPage, MenuScene.SceneID.Landscape_SS));
    mainPage.subObjects.Add(backObject = new SimpleButton(this, mainPage, "BACK", "BACK", new(30f, screenY - 60f), new(110f, 30f)));
    mainPage.subObjects.Add(new MenuLabel(this, mainPage, "CHANGELOGS", new(screenX / 2f, screenY - 40f), new(0f, 0f), true));

    rectBack = new(mainPage.Container, new(10f, 90f), new(screenX - 20f, screenY - 160f), true);
    rowLimit = (int)(rectBack.size.y / 16f);
    mainPage.subObjects.Add(new BigArrowButton(this, mainPage, "PREV", new(20f, 20f), 3));
    mainPage.subObjects.Add(new BigArrowButton(this, mainPage, "NEXT", new(screenX - 70f, 20f), 1));

    mainPage.subObjects.Add(new BigArrowButton(this, mainPage, "PAGEDOWN", new(screenX - 70f, 100f), 2));
    mainPage.subObjects.Add(new BigArrowButton(this, mainPage, "PAGEUP", new(screenX - 70f, 160f), 0));

    mainPage.subObjects.Add(modName = new(this, mainPage, "", new(screenX / 2f, 50f), new(0f, 0f), true));
    mainPage.subObjects.Add(changelog = new(this, mainPage, "", new(30f, screenY - 90f), new(0f, 0f), false));
    changelog.label.alignment = FLabelAlignment.Left;
    changelog.label._anchorY = 1f;
    UpdateSelectedChangelog();
  }

  public void UpdateSelectedChangelog()
  {
    viewLevel = 0;
    if (changelogList.Count == 0)
    {
      modName.text = "<No changelogs available - something went horribly wrong>";
      return;
    }
    selectedChangelog = changelogList[changelogIndex];
    if (selectedChangelog.updated)
    {
      modName.text = selectedChangelog.modLabel + " [UPDATED]";
      modName.label.color = new(1f, 0.95f, 0.4f);
    }
    else
    {
      modName.text = selectedChangelog.modLabel;
      modName.label.color = Color.white;
    }
    UpdateChangelogView();
  }

  public void UpdateChangelogView()
  {
    if (rowLimit >= selectedChangelog.lineIndexes.Count)
      changelog.text = selectedChangelog.changelog;
    else if (rowLimit + viewLevel >= selectedChangelog.lineIndexes.Count)
      changelog.text = selectedChangelog.changelog.Substring(selectedChangelog.lineIndexes[viewLevel]);
    else
      changelog.text = selectedChangelog.changelog.SubstringUntil(selectedChangelog.lineIndexes[viewLevel], selectedChangelog.lineIndexes[viewLevel + rowLimit]);
  }

  public void NormalizeViewLevel()
  {
    if (viewLevel < 0)
      viewLevel = 0;
    else if (viewLevel >= selectedChangelog.lineIndexes.Count - rowLimit + 8)
      viewLevel = selectedChangelog.lineIndexes.Count - rowLimit + 8;
  }

  public override void Singal(MenuObject sender, string message)
  {
    if (message == "BACK")
    {
      manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
      PlaySound(SoundID.MENU_Switch_Page_Out);
      return;
    }
    if (changelogList.Count == 0)
      return;

    int oldLevel = viewLevel;
    switch (message)
    {
      case "PREV":
        changelogIndex = (changelogIndex + changelogList.Count - 1) % changelogList.Count;
        UpdateSelectedChangelog();
        break;
      case "NEXT":
        changelogIndex = (changelogIndex + 1) % changelogList.Count;
        UpdateSelectedChangelog();
        break;
      case "PAGEDOWN":
        viewLevel += rowLimit;
        NormalizeViewLevel();
        if (oldLevel != viewLevel)
          UpdateChangelogView();
        break;
      case "PAGEUP":
        viewLevel -= rowLimit;
        NormalizeViewLevel();
        if (oldLevel != viewLevel)
          UpdateChangelogView();
        break;
    }
    PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
  }

  public override void Update()
  {
    base.Update();
    rectBack.Update();
    if (changelogList.Count == 0 || selectedChangelog.lineIndexes.Count <= rowLimit || Input.mouseScrollDelta.y == 0f)
      return;
    int oldLevel = viewLevel;
    viewLevel -= (int)Mathf.Sign(Input.mouseScrollDelta.y);
    NormalizeViewLevel();
    if (oldLevel != viewLevel)
      UpdateChangelogView();
  }

  public override void GrafUpdate(float timeStacker)
  {
    base.GrafUpdate(timeStacker);
    rectBack.GrafUpdate(timeStacker);
  }
}