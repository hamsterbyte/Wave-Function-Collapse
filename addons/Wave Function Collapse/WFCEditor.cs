/*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
|||||||||||||||||||||||||||||||||||||||||||||| MIT LICENSE ||||||||||||||||||||||||||||||||||||||||||||||||||||
Copyright 2023 hamsterbyte
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the “Software”), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions
of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
/////////////////////////////////////////////////////////////////////////////////////////////////////////////*/

#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Godot.Collections;
using hamsterbyte.WFC;

[Tool]
public partial class WFCEditor : EditorPlugin{
    #region VARIABLES

    private PackedScene _editorPanel =
        ResourceLoader.Load<PackedScene>("res://addons/Wave Function Collapse/WFCEditor.tscn");

    private Control _editorPanelInstance;

    //Controls
    private Button _btnCreateNew;
    private FileDialog _dlgCreateNew, _dlgImportTileset, _dlgImportRules;
    private TilesList _tilesList;
    private TextureRect _selectedTile, _upTile, _rightTile, _downTile, _leftTile;
    private FlowContainer _upTilesFlow, _rightTilesFlow, _downTilesFlow, _leftTilesFlow;

    private Button _btnImport;

    private Button _btnExport;
    private FileDialog _dlgExport;

    private CheckButton[] _upTileCheckButtons;
    private CheckButton[] _rightTileCheckButtons;
    private CheckButton[] _downTileCheckButtons;
    private CheckButton[] _leftTileCheckButtons;

    private Texture2D _upArrowTexture, _rightArrowTexture, _downArrowTexture, _leftArrowTexture;


    private TileSetAtlasSource _atlasSource;
    private AtlasTexture[] _tileTextures;
    private List<WFCRule> _rules;
    private SpinBox _frequency;
    private Button _applyFrequencyButton;

    private string tilesetPathTemp;
    private int lastSelectedTileIndex = -1;

    #endregion

    #region MAIN

    public override void _EnterTree(){
        Init();
    }

    public override void _ExitTree(){
        _editorPanelInstance.QueueFree();
    }

    public override bool _HasMainScreen(){
        return true;
    }

    public override void _MakeVisible(bool visible){
        if (_editorPanelInstance != null){
            _editorPanelInstance.Visible = visible;
        }
    }

    public override string _GetPluginName(){
        return "WFC Editor";
    }

    public override Texture2D _GetPluginIcon(){
        return GetEditorInterface().GetBaseControl().GetThemeIcon("ResourcePreloader", "EditorIcons");
    }

    #endregion

    #region SETUP

    private void Init(){
        _editorPanelInstance = (Control)_editorPanel.Instantiate();
        GetEditorInterface().GetEditorMainScreen().AddChild(_editorPanelInstance);
        _MakeVisible(false);

        //Load Arrow Textures
        _upArrowTexture = ResourceLoader.Load<Texture2D>("res://addons/Wave Function Collapse/sprite_UpArrow.tres");
        _rightArrowTexture =
            ResourceLoader.Load<Texture2D>("res://addons/Wave Function Collapse/sprite_RightArrow.tres");
        _downArrowTexture = ResourceLoader.Load<Texture2D>("res://addons/Wave Function Collapse/sprite_DownArrow.tres");
        _leftArrowTexture = ResourceLoader.Load<Texture2D>("res://addons/Wave Function Collapse/sprite_LeftArrow.tres");

        GatherRequirements();
        SetupCallbacks();
    }

    private void GatherRequirements(){
        _btnCreateNew = _editorPanelInstance.GetNode("VBoxContainer/HBoxContainer/btn_New") as Button;
        _dlgCreateNew = _editorPanelInstance.GetNode("VBoxContainer/HBoxContainer/btn_New/dlg_New") as FileDialog;

        _btnImport = _editorPanelInstance.GetNode("VBoxContainer/HBoxContainer/btn_Import") as Button;
        _dlgImportTileset =
            _editorPanelInstance.GetNode("VBoxContainer/HBoxContainer/btn_Import/dlg_ImportTileset") as FileDialog;
        _dlgImportRules =
            _editorPanelInstance.GetNode("VBoxContainer/HBoxContainer/btn_Import/dlg_ImportRules") as FileDialog;

        _btnExport = _editorPanelInstance.GetNode("VBoxContainer/btn_Export") as Button;
        _dlgExport = _editorPanelInstance.GetNode("VBoxContainer/btn_Export/dlg_Export") as FileDialog;

        _tilesList = _editorPanelInstance.GetNode(
            "VBoxContainer/pnl_TileSelection/VBoxContainer/ItemList") as TilesList;

        _selectedTile =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/grid_centerButtons/selectedTile") as
                TextureRect;

        _upTilesFlow =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/upTilesScroll/tc") as FlowContainer;
        _rightTilesFlow =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/rightTilesScroll/cr") as FlowContainer;
        _downTilesFlow =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/downTilesScroll/bc") as FlowContainer;
        _leftTilesFlow =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/leftTilesScroll/cl") as FlowContainer;

        _upTile =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/grid_centerButtons/upTile") as
                TextureRect;
        _rightTile =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/grid_centerButtons/rightTile") as
                TextureRect;
        _downTile =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/grid_centerButtons/downTile") as
                TextureRect;
        _leftTile =
            _editorPanelInstance.GetNode("pnl_Contents/margin_Contents/grid_main/grid_centerButtons/leftTile") as
                TextureRect;

        _frequency = _editorPanelInstance.GetNode("VBoxContainer/hBox_Freq/SpinBox") as SpinBox;
        _applyFrequencyButton = _editorPanelInstance.GetNode("VBoxContainer/hBox_Freq/Button") as Button;
    }


    private void SetupCallbacks(){
        _tilesList.onDropData += OnDataDropped;
        _btnCreateNew.Pressed += ShowCreateNewDialog;
        _dlgCreateNew.FileSelected += CreateNew;
        _tilesList.ItemSelected += ChangeSelection;
        _btnExport.Pressed += ShowExportDialog;
        _dlgExport.FileSelected += ExportJSON;
        _btnImport.Pressed += ShowImportTilesetDialog;
        _dlgImportTileset.FileSelected += SetTempTilesetPath;
        _dlgImportRules.FileSelected += Import;
        _applyFrequencyButton.Pressed += ModifyFrequency;
    }

    private void OnDataDropped(NodePath path){
        TileMap tileMap = GetNode(path) as TileMap;
        if (tileMap == null){
            GD.PrintErr("Drag/Drop Tilemap node to analyze. Other types not supported.");
            return;
        }

        AnalyzeTileMap(tileMap);
    }

    /*/////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ANALYZE TILEMAP => AUTOMATICALLY GENERATE CONSTRAINTS FROM A TILEMAP; THIS IS THE PREFERRED METHOD
    THIS METHOD CREATES FUNCTIONALITY WHEREBY YOU CAN SIMPLY PAINT A TILEMAP, DRAG THAT TILEMAP FROM THE SCENE INTO
    THE TILES LIST IN THE WFCEDITOR WINDOW, AND HAVE IT AUTOMATICALLY ANALYZED TO CREATE ADJACENCY AND FREQUENCY
    RULES. THIS IS THE MAIN METHOD FOR SETTING UP THE WFC GENERATION CONSTRAINTS, BUT METHODS FOR CREATING
    CONSTRAINTS FROM SCRATCH, OR MODIFYING CONSTRAINTS HAVE ALSO BEEN IMPLEMENTED FOR YOUR CONVENIENCE

         **YOU WILL STILL HAVE TO EXPORT THE RULES TO A JSON FILE AND PASS THEM TO THE WFCGENERATOR SCRIPT**

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////*/
    private void AnalyzeTileMap(TileMap tileMap){
        //Get atlas source with pattern matching; if no atlas present error and return
        if (tileMap.TileSet.GetSource(0) is not TileSetAtlasSource _atlas){
            GD.PrintErr("Atlas not found.");
            return;
        }

        _atlasSource = _atlas;

        //Get used cells; if no cells used, error and return
        Array<Vector2I> usedCells = tileMap.GetUsedCells(0);
        if (usedCells.Count == 0){
            GD.PrintErr(
                "TileMap contains no used cells. Paint tilemap in the style you want to generate before analyzing.");
            return;
        }

        //Get max boundaries of used cells to set up grid
        int xMax = 0;
        int yMax = 0;
        foreach (Vector2I cell in usedCells){
            xMax = cell.X > xMax ? cell.X : xMax;
            yMax = cell.Y > yMax ? cell.Y : yMax;
        }

        //Get Tilemap
        TileSet t = tileMap.TileSet;

        //Set Tile Size
        Vector2I tileSize = t.TileSize;

        //Get Tile Count
        int tileCount = _atlas.GetTilesCount();
        if (tileCount == 0) return;

        //Initialize Frequency Array
        int[] frequencies = new int[tileCount];

        //Initialize Texture Array
        _tileTextures = new AtlasTexture[tileCount];


        //Get Tile ID's by Index
        System.Collections.Generic.Dictionary<Vector2I, int> tileIndices = new();
        for (int i = 0; i < _atlas.GetTilesCount(); i++){
            tileIndices.TryAdd(_atlas.GetTileId(i), i);
        }

        _rules = new List<WFCRule>();

        _tilesList.Clear();
        for (int i = 0; i < _atlas.GetTilesCount(); i++){
            AtlasTexture tex = new();
            Vector2I coords = _atlas.GetTileId(i);
            tileIndices.TryAdd(coords, i);
            tex.Atlas = _atlas.Texture;
            tex.Region = new Rect2(coords.X * tileSize.X, coords.Y * tileSize.Y, tileSize.X, tileSize.Y);
            _tileTextures[i] = tex;
            _tilesList.AddItem($"ID: {i} {coords.ToString()}", _tileTextures[i]);
            _rules.Add(new WFCRule());
        }

        //Calculate height and width
        int width = xMax + 1;
        int height = yMax + 1;
        //Get frequencies and neighbour indices
        for (int x = 0; x < width; x++){
            for (int y = 0; y < height; y++){
                Vector2I atlasCoords = tileMap.GetCellAtlasCoords(0, new Vector2I(x, y));
                if (!tileIndices.TryGetValue(atlasCoords, out int index)) continue;
                frequencies[index]++;
                Vector2I[] neighbours ={
                    tileMap.GetCellAtlasCoords(0,
                        tileMap.GetNeighborCell(new Vector2I(x, y), TileSet.CellNeighbor.TopSide)),
                    tileMap.GetCellAtlasCoords(0,
                        tileMap.GetNeighborCell(new Vector2I(x, y), TileSet.CellNeighbor.RightSide)),
                    tileMap.GetCellAtlasCoords(0,
                        tileMap.GetNeighborCell(new Vector2I(x, y), TileSet.CellNeighbor.BottomSide)),
                    tileMap.GetCellAtlasCoords(0,
                        tileMap.GetNeighborCell(new Vector2I(x, y), TileSet.CellNeighbor.LeftSide))
                };

                //Check validity of vectors and gather indices
                Vector2I invalidVector = new(-1, -1);
                int upNeighbourIndex = neighbours[0] != invalidVector ? tileIndices[neighbours[0]] : -1;
                int rightNeighbourIndex = neighbours[1] != invalidVector ? tileIndices[neighbours[1]] : -1;
                int downNeighbourIndex = neighbours[2] != invalidVector ? tileIndices[neighbours[2]] : -1;
                int leftNeighbourIndex = neighbours[3] != invalidVector ? tileIndices[neighbours[3]] : -1;


                if (upNeighbourIndex >= 0){
                    if (!_rules[index].Options.Up.Contains(upNeighbourIndex))
                        _rules[index].Options.Up.Add(upNeighbourIndex);
                    if (!_rules[upNeighbourIndex].Options.Down.Contains(index))
                        _rules[upNeighbourIndex].Options.Down.Add(index);
                }


                if (rightNeighbourIndex >= 0){
                    if (!_rules[index].Options.Right.Contains(rightNeighbourIndex))
                        _rules[index].Options.Right.Add(rightNeighbourIndex);
                    if (!_rules[rightNeighbourIndex].Options.Left.Contains(index))
                        _rules[rightNeighbourIndex].Options.Left.Add(index);
                }

                if (downNeighbourIndex >= 0){
                    if (!_rules[index].Options.Down.Contains(downNeighbourIndex))
                        _rules[index].Options.Down.Add(downNeighbourIndex);
                    if (!_rules[downNeighbourIndex].Options.Up.Contains(index))
                        _rules[downNeighbourIndex].Options.Up.Add(index);
                }

                if (leftNeighbourIndex >= 0){
                    if (!_rules[index].Options.Left.Contains(leftNeighbourIndex))
                        _rules[index].Options.Left.Add(leftNeighbourIndex);
                    if (!_rules[leftNeighbourIndex].Options.Right.Contains(index))
                        _rules[leftNeighbourIndex].Options.Right.Add(index);
                }

                //Apply Frequencies to rules
                for (int i = 0; i < frequencies.Length; i++){
                    _rules[i].Frequency = frequencies[i];
                }
            }
        }

        CreateUpTileOptions();
        CreateRightTileOptions();
        CreateDownTileOptions();
        CreateLeftTileOptions();

        _tilesList.Select(0);
        ChangeSelection(0);
    }


    private void SetTempTilesetPath(string path){
        tilesetPathTemp = path;
        _dlgImportTileset.Visible = false;
        ShowImportRulesDialog();
    }

    private void ShowImportRulesDialog(){
        _dlgImportRules.Visible = true;
    }

    private void ShowImportTilesetDialog(){
        _dlgImportTileset.Visible = true;
    }

    private void ExportJSON(string path){
        string globalPath = ProjectSettings.GlobalizePath(path);
        using StreamWriter s = new(globalPath);
        s.Write(_rules.ToJSON());
        s.Close();
    }

    private void ShowExportDialog(){
        if (_tilesList.ItemCount == 0) return;
        _dlgExport.Visible = true;
    }

    #endregion


    private void ShowCreateNewDialog(){
        _dlgCreateNew.Visible = true;
    }

    private void CreateNew(string path){
        try{
            TileSet t = ResourceLoader.Load<TileSet>(path);
            _tilesList.Clear();
            Vector2I tileSize = t.TileSize;
            _atlasSource = t.GetSource(0) as TileSetAtlasSource;
            if (_atlasSource == null) return;
            int tileCount = _atlasSource.GetTilesCount();
            if (tileCount == 0) return;
            _tileTextures = new AtlasTexture[tileCount];

            _rules = new List<WFCRule>();
            for (int i = 0; i < _atlasSource.GetTilesCount(); i++){
                AtlasTexture tex = new();
                Vector2I coords = _atlasSource.GetTileId(i);
                tex.Atlas = _atlasSource.Texture;
                tex.Region = new Rect2(coords.X * tileSize.X, coords.Y * tileSize.Y, tileSize.X, tileSize.Y);
                _tileTextures[i] = tex;
                _tilesList.AddItem($"ID: {i} @ {coords.ToString()}", _tileTextures[i]);
                _rules.Add(new WFCRule());
            }

            CreateUpTileOptions();
            CreateRightTileOptions();
            CreateDownTileOptions();
            CreateLeftTileOptions();

            _tilesList.Select(0);
            ChangeSelection(0);
        }
        catch (InvalidCastException e){
            GD.PrintErr($"Invalid Resource: {e.Message}\nPlease make sure to select a Tileset resource.");
        }
    }

    private void Import(string rulePath){
        try{
            TileSet t = ResourceLoader.Load<TileSet>(tilesetPathTemp);
            tilesetPathTemp = string.Empty;
            _tilesList.Clear();
            Vector2I tileSize = t.TileSize;
            _atlasSource = t.GetSource(0) as TileSetAtlasSource;
            if (_atlasSource == null) return;
            int tileCount = _atlasSource.GetTilesCount();
            if (tileCount == 0) return;
            _tileTextures = new AtlasTexture[tileCount];
            for (int i = 0; i < _atlasSource.GetTilesCount(); i++){
                AtlasTexture tex = new();
                Vector2I coords = _atlasSource.GetTileId(i);
                tex.Atlas = _atlasSource.Texture;
                tex.Region = new Rect2(coords.X * tileSize.X, coords.Y * tileSize.Y, tileSize.X, tileSize.Y);
                _tileTextures[i] = tex;
                _tilesList.AddItem($"ID: {i} {coords.ToString()}", _tileTextures[i]);
            }

            CreateUpTileOptions();
            CreateRightTileOptions();
            CreateDownTileOptions();
            CreateLeftTileOptions();


            _rules = WFCRule.FromJSONFile(ProjectSettings.GlobalizePath(rulePath));

            _tilesList.Select(0);
            ChangeSelection(0);
        }
        catch (InvalidCastException e){
            GD.PrintErr($"Invalid Resource: {e.Message}\nPlease make sure to select a Tileset resource.");
        }
    }

    #region UP TILES

    private void ToggleUpRule(bool toggled){
        int controlIndex = IsOverControl(_upTileCheckButtons);
        if (controlIndex <= -1) return;
        _rules[SelectedTileIndex].Options.Toggle(NeighbourDirections.Up, controlIndex, toggled);
    }

    private void LoadUpTileOptions(){
        foreach (CheckButton b in _upTileCheckButtons){
            b.ButtonPressed = false;
        }

        List<int> _options = _rules[SelectedTileIndex].Options.Up;
        foreach (int i in _options){
            _upTileCheckButtons[i].ButtonPressed = true;
        }
    }

    private void ChangeUpTile(){
        int controlIndex = IsOverControl(_upTileCheckButtons);
        if (controlIndex > -1){
            _upTile.Texture = _upTileCheckButtons[controlIndex].Icon;
        }
    }

    private void ResetUpTile(){
        _upTile.Texture = _upArrowTexture;
    }

    private void ClearUpTileOptions(){
        if (_upTilesFlow.GetChildCount() <= 0) return;
        for (int i = 0; i < _upTilesFlow.GetChildCount(); i++){
            _upTilesFlow.GetChild(i).QueueFree();
        }
    }

    private void CreateUpTileOptions(){
        ClearUpTileOptions();
        int tileCount = _atlasSource.GetTilesCount();
        _upTileCheckButtons = new CheckButton[tileCount];
        for (int i = 0; i < tileCount; i++){
            CreateUpTileOption(i);
        }
    }

    private void CreateUpTileOption(int index){
        CheckButton tButton = new();
        tButton.Icon = _tileTextures[index];
        tButton.IconAlignment = HorizontalAlignment.Left;
        tButton.ExpandIcon = true;
        tButton.CustomMinimumSize = new Vector2(128, 64);
        tButton.MouseEntered += ChangeUpTile;
        tButton.MouseExited += ResetUpTile;
        tButton.Toggled += ToggleUpRule;
        _upTileCheckButtons[index] = tButton;
        _upTilesFlow.AddChild(tButton);
    }

    #endregion

    #region RIGHT TILES

    private void ToggleRightRule(bool toggled){
        int controlIndex = IsOverControl(_rightTileCheckButtons);
        if (controlIndex <= -1) return;
        _rules[SelectedTileIndex].Options.Toggle(NeighbourDirections.Right, controlIndex, toggled);
    }

    private void LoadRightTileOptions(){
        foreach (CheckButton b in _rightTileCheckButtons){
            b.ButtonPressed = false;
        }

        List<int> _options = _rules[SelectedTileIndex].Options.Right;
        foreach (int i in _options){
            _rightTileCheckButtons[i].ButtonPressed = true;
        }
    }

    private void ChangeRightTile(){
        int controlIndex = IsOverControl(_rightTileCheckButtons);
        if (controlIndex > -1){
            _rightTile.Texture = _rightTileCheckButtons[controlIndex].Icon;
        }
    }

    private void ResetRightTile(){
        _rightTile.Texture = _rightArrowTexture;
    }

    private void ClearRightTileOptions(){
        if (_rightTilesFlow.GetChildCount() <= 0) return;
        for (int i = 0; i < _rightTilesFlow.GetChildCount(); i++){
            _rightTilesFlow.GetChild(i).QueueFree();
        }
    }

    private void CreateRightTileOptions(){
        ClearRightTileOptions();
        int tileCount = _atlasSource.GetTilesCount();
        _rightTileCheckButtons = new CheckButton[tileCount];
        for (int i = 0; i < tileCount; i++){
            CreateRightTileOption(i);
        }
    }

    private void CreateRightTileOption(int index){
        CheckButton tButton = new();
        tButton.Icon = _tileTextures[index];
        tButton.IconAlignment = HorizontalAlignment.Left;
        tButton.ExpandIcon = true;
        tButton.CustomMinimumSize = new Vector2(128, 64);
        tButton.MouseEntered += ChangeRightTile;
        tButton.MouseExited += ResetRightTile;
        tButton.Toggled += ToggleRightRule;
        _rightTileCheckButtons[index] = tButton;
        _rightTilesFlow.AddChild(tButton);
    }

    #endregion

    #region DOWN TILES

    private void ToggleDownRule(bool toggled){
        int controlIndex = IsOverControl(_downTileCheckButtons);
        if (controlIndex <= -1) return;
        _rules[SelectedTileIndex].Options.Toggle(NeighbourDirections.Down, controlIndex, toggled);
    }

    private void LoadDownTileOptions(){
        foreach (CheckButton b in _downTileCheckButtons){
            b.ButtonPressed = false;
        }

        List<int> _options = _rules[SelectedTileIndex].Options.Down;
        foreach (int i in _options){
            _downTileCheckButtons[i].ButtonPressed = true;
        }
    }

    private void ChangeDownTile(){
        int controlIndex = IsOverControl(_downTileCheckButtons);
        if (controlIndex <= -1) return;
        _downTile.Texture = _downTileCheckButtons[controlIndex].Icon;
        _downTile.Texture = _downTileCheckButtons[controlIndex].Icon;
    }

    private void ResetDownTile(){
        _downTile.Texture = _downArrowTexture;
    }

    private void ClearDownTileOptions(){
        if (_downTilesFlow.GetChildCount() <= 0) return;
        for (int i = 0; i < _downTilesFlow.GetChildCount(); i++){
            _downTilesFlow.GetChild(i).QueueFree();
        }
    }

    private void CreateDownTileOptions(){
        ClearDownTileOptions();
        int tileCount = _atlasSource.GetTilesCount();
        _downTileCheckButtons = new CheckButton[tileCount];
        for (int i = 0; i < tileCount; i++){
            CreateDownTileOption(i);
        }
    }

    private void CreateDownTileOption(int index){
        CheckButton tButton = new();
        tButton.Icon = _tileTextures[index];
        tButton.IconAlignment = HorizontalAlignment.Left;
        tButton.ExpandIcon = true;
        tButton.CustomMinimumSize = new Vector2(128, 64);
        tButton.MouseEntered += ChangeDownTile;
        tButton.MouseExited += ResetDownTile;
        tButton.Toggled += ToggleDownRule;
        _downTileCheckButtons[index] = tButton;
        _downTilesFlow.AddChild(tButton);
    }

    #endregion

    #region LEFT TILES

    private void ToggleLeftRule(bool toggled){
        int controlIndex = IsOverControl(_leftTileCheckButtons);
        if (controlIndex <= -1) return;
        _rules[SelectedTileIndex].Options.Toggle(NeighbourDirections.Left, controlIndex, toggled);
    }

    private void LoadLeftTileOptions(){
        foreach (CheckButton b in _leftTileCheckButtons){
            b.ButtonPressed = false;
        }

        List<int> _options = _rules[SelectedTileIndex].Options.Left;
        foreach (int i in _options){
            _leftTileCheckButtons[i].ButtonPressed = true;
        }
    }

    private void ChangeLeftTile(){
        int controlIndex = IsOverControl(_leftTileCheckButtons);
        if (controlIndex > -1){
            _leftTile.Texture = _leftTileCheckButtons[controlIndex].Icon;
        }
    }

    private void ResetLeftTile(){
        _leftTile.Texture = _leftArrowTexture;
    }

    private void ClearLeftTileOptions(){
        if (_leftTilesFlow.GetChildCount() <= 0) return;
        for (int i = 0; i < _leftTilesFlow.GetChildCount(); i++){
            _leftTilesFlow.GetChild(i).QueueFree();
        }
    }

    private void CreateLeftTileOptions(){
        ClearLeftTileOptions();
        int tileCount = _atlasSource.GetTilesCount();
        _leftTileCheckButtons = new CheckButton[tileCount];
        for (int i = 0; i < tileCount; i++){
            CreateLeftTileOption(i);
        }
    }

    private void CreateLeftTileOption(int index){
        CheckButton tButton = new();
        tButton.Icon = _tileTextures[index];
        tButton.IconAlignment = HorizontalAlignment.Left;
        tButton.ExpandIcon = true;
        tButton.CustomMinimumSize = new Vector2(128, 64);
        tButton.MouseEntered += ChangeLeftTile;
        tButton.MouseExited += ResetLeftTile;
        tButton.Toggled += ToggleLeftRule;
        _leftTileCheckButtons[index] = tButton;
        _leftTilesFlow.AddChild(tButton);
    }

    #endregion

    #region HELPERS

    private int IsOverControl(Control[] controlArray){
        for (int i = 0; i < controlArray.Length; i++){
            Vector2 mousePosition = GetViewport().GetMousePosition();
            Rect2 testRect = controlArray[i].GetGlobalRect();
            if (testRect.HasPoint(mousePosition)) return i;
        }

        return -1;
    }

    private void ChangeSelection(long index){
        _selectedTile.Texture = _tileTextures[index];
        LoadUpTileOptions();
        LoadRightTileOptions();
        LoadDownTileOptions();
        LoadLeftTileOptions();
        _frequency.Apply();
        _frequency.SetValueNoSignal(_rules[SelectedTileIndex].Frequency);
        _frequency.Value = _rules[(int)index].Frequency;
    }

    private void ModifyFrequency(){
        if (_tilesList.ItemCount == 0) return;
        if (_rules[SelectedTileIndex].Frequency != (int)_frequency.Value){
            _rules[SelectedTileIndex].Frequency = (int)_frequency.Value;
        }
    }

    private int SelectedTileIndex => _tilesList.GetSelectedItems()[0];

    #endregion
}
#endif