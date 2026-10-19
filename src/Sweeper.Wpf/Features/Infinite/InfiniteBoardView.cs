using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Sweeper.Core.Features.Infinite;
using Sweeper.Core.Shared;
using Sweeper.Wpf.Features.Board;

namespace Sweeper.Wpf.Features.Infinite;

// Desenha direto no DrawingContext só o que cabe na tela: num mapa infinito não dá pra ter um elemento por célula.
// Clique abre, botão direito põe bandeira, arrastar move o mapa e a roda do mouse dá zoom.
public sealed class InfiniteBoardView : FrameworkElement
{
  // Abaixo disso as células viram pixels: desenha só o mapa dos setores.
  private const double MapThreshold = 7;

  // Número e ícones só quando cabem.
  private const double DetailThreshold = 14;

  // Até aqui o mouse mexeu sem querer durante o clique; passou disso é arrasto.
  private const double DragThreshold = 5;

  private const double WheelZoomStep = 1.2;
  private const double KeyPanStep = 96;

  public static readonly DependencyProperty GameProperty = DependencyProperty.Register(
    nameof(Game), typeof(InfiniteGame), typeof(InfiniteBoardView),
    new PropertyMetadata(null, (d, e) => ((InfiniteBoardView)d).OnSourceChanged(e)));

  public static readonly DependencyProperty CameraProperty = DependencyProperty.Register(
    nameof(Camera), typeof(Camera), typeof(InfiniteBoardView),
    new PropertyMetadata(null, (d, e) => ((InfiniteBoardView)d).OnSourceChanged(e)));

  public static readonly DependencyProperty RevealCommandProperty = DependencyProperty.Register(
    nameof(RevealCommand), typeof(ICommand), typeof(InfiniteBoardView));

  public static readonly DependencyProperty FlagCommandProperty = DependencyProperty.Register(
    nameof(FlagCommand), typeof(ICommand), typeof(InfiniteBoardView));

  private readonly Dictionary<int, FormattedText> _numbers = [];
  private double _numbersFontSize;

  private Brush _hidden = Brushes.LightGray;
  private Brush _hiddenHover = Brushes.Gray;
  private Brush _open = Brushes.White;
  private Brush _flag = Brushes.Red;
  private Brush _danger = Brushes.Red;
  private Brush _solvedOverlay = Brushes.Transparent;
  private Brush _lockedOverlay = Brushes.Transparent;
  private Brush _solvedMap = Brushes.LightGreen;
  private Brush _lockedMap = Brushes.Pink;
  private Brush _rangeFill = Brushes.LightYellow;
  private Pen _rangePen = new(Brushes.Orange, 2);
  private Brush _sectorLine = Brushes.Gray;
  private Geometry _flagGeometry = Geometry.Empty;
  private Geometry _lockGeometry = Geometry.Empty;
  private ImageSource? _bomb;

  private GridPosition? _hover;
  private MouseButton? _dragButton;
  private bool _isPanning;
  private Point _pressPoint;
  private Point _lastPoint;

  public InfiniteBoardView()
  {
    Focusable = true;
    FocusVisualStyle = null;
    ClipToBounds = true;

    Loaded += (_, _) =>
    {
      LoadResources();
      Attach(Game, Camera);
      InvalidateVisual();
      Focus();
    };

    // Sem isso a tela antiga continuaria ouvindo o jogo depois de trocar pro clássico.
    Unloaded += (_, _) => Detach(Game, Camera);
  }

  public InfiniteGame? Game
  {
    get => (InfiniteGame?)GetValue(GameProperty);
    set => SetValue(GameProperty, value);
  }

  public Camera? Camera
  {
    get => (Camera?)GetValue(CameraProperty);
    set => SetValue(CameraProperty, value);
  }

  // Parâmetro: a GridPosition da célula.
  public ICommand? RevealCommand
  {
    get => (ICommand?)GetValue(RevealCommandProperty);
    set => SetValue(RevealCommandProperty, value);
  }

  public ICommand? FlagCommand
  {
    get => (ICommand?)GetValue(FlagCommandProperty);
    set => SetValue(FlagCommandProperty, value);
  }

  protected override void OnRender(DrawingContext dc)
  {
    var bounds = new Rect(RenderSize);

    if (Game is not { } game || Camera is not { } camera)
    {
      // Fundo transparente pra receber clique mesmo vazio.
      dc.DrawRectangle(Brushes.Transparent, null, bounds);
      return;
    }

    var view = new Viewport(camera, RenderSize);

    if (view.Size < MapThreshold)
    {
      DrawMap(dc, game, view, bounds);
      return;
    }

    // Fundo na cor das abertas; as fechadas vêm num pincel ladrilhado, um ladrilho por célula.
    dc.DrawRectangle(_open, null, bounds);
    dc.DrawRectangle(HiddenTiles(view), null, bounds);

    foreach (var sector in VisibleSectors(game, view))
    {
      DrawSectorCells(dc, game, sector, view);
    }

    DrawHover(dc, game, view);

    foreach (var sector in VisibleSectors(game, view))
    {
      DrawSectorOverlay(dc, sector, view);
    }

    DrawSectorLines(dc, view, bounds);
  }

  protected override void OnMouseDown(MouseButtonEventArgs e)
  {
    base.OnMouseDown(e);
    Focus();

    var point = e.GetPosition(this);

    if (e.ChangedButton is MouseButton.Left or MouseButton.Middle && _dragButton is null)
    {
      // O clique esquerdo só vale no MouseUp, porque até lá pode virar arrasto.
      _dragButton = e.ChangedButton;
      _pressPoint = _lastPoint = point;
      _isPanning = e.ChangedButton == MouseButton.Middle;
      CaptureMouse();

      if (_isPanning)
      {
        Cursor = Cursors.SizeAll;
      }
    }
    else if (e.ChangedButton == MouseButton.Right && !_isPanning)
    {
      ExecuteAt(FlagCommand, CellAt(point));
    }

    e.Handled = true;
  }

  protected override void OnMouseMove(MouseEventArgs e)
  {
    base.OnMouseMove(e);

    var point = e.GetPosition(this);

    if (_dragButton is not null)
    {
      if (!_isPanning && (point - _pressPoint).Length > DragThreshold)
      {
        _isPanning = true;
        Cursor = Cursors.SizeAll;
      }

      if (_isPanning)
      {
        Camera?.PanBy(point - _lastPoint);
      }

      _lastPoint = point;
    }

    SetHover(_isPanning ? null : CellAt(point));
  }

  protected override void OnMouseUp(MouseButtonEventArgs e)
  {
    base.OnMouseUp(e);

    if (e.ChangedButton != _dragButton)
    {
      return;
    }

    var wasPanning = _isPanning;
    EndDrag();

    if (!wasPanning && e.ChangedButton == MouseButton.Left)
    {
      ExecuteAt(RevealCommand, CellAt(e.GetPosition(this)));
    }

    e.Handled = true;
  }

  // Alt+Tab ou outra janela no meio do arrasto: larga sem clicar.
  protected override void OnLostMouseCapture(MouseEventArgs e)
  {
    base.OnLostMouseCapture(e);
    _dragButton = null;
    _isPanning = false;
    Cursor = null;
  }

  protected override void OnMouseLeave(MouseEventArgs e)
  {
    base.OnMouseLeave(e);
    SetHover(null);
  }

  protected override void OnMouseWheel(MouseWheelEventArgs e)
  {
    base.OnMouseWheel(e);

    var point = e.GetPosition(this);
    Camera?.ZoomAt(Math.Pow(WheelZoomStep, e.Delta / 120.0), point, RenderSize);
    SetHover(CellAt(point));
    e.Handled = true;
  }

  // Setas/WASD movem, +/- dão zoom, espaço abre e F põe bandeira na célula sob o mouse.
  protected override void OnKeyDown(KeyEventArgs e)
  {
    base.OnKeyDown(e);

    if (Camera is not { } camera)
    {
      return;
    }

    var center = new Point(RenderSize.Width / 2, RenderSize.Height / 2);

    switch (e.Key)
    {
      case Key.Left or Key.A:
        camera.PanBy(new Vector(KeyPanStep, 0));
        break;
      case Key.Right or Key.D:
        camera.PanBy(new Vector(-KeyPanStep, 0));
        break;
      case Key.Up or Key.W:
        camera.PanBy(new Vector(0, KeyPanStep));
        break;
      case Key.Down or Key.S:
        camera.PanBy(new Vector(0, -KeyPanStep));
        break;
      case Key.OemPlus or Key.Add:
        camera.ZoomAt(WheelZoomStep, center, RenderSize);
        break;
      case Key.OemMinus or Key.Subtract:
        camera.ZoomAt(1 / WheelZoomStep, center, RenderSize);
        break;
      case Key.Space when _hover is { } cell:
        Execute(RevealCommand, cell);
        break;
      case Key.F when _hover is { } cell:
        Execute(FlagCommand, cell);
        break;
      default:
        return;
    }

    e.Handled = true;
  }

  private static void Execute(ICommand? command, GridPosition cell)
  {
    if (command?.CanExecute(cell) == true)
    {
      command.Execute(cell);
    }
  }

  private void EndDrag()
  {
    _dragButton = null;
    _isPanning = false;
    Cursor = null;
    ReleaseMouseCapture();
  }

  private GridPosition? CellAt(Point point)
  {
    if (Camera is not { } camera)
    {
      return null;
    }

    var world = camera.ScreenToWorld(point, RenderSize);
    return new GridPosition((int)Math.Floor(world.X), (int)Math.Floor(world.Y));
  }

  private static void ExecuteAt(ICommand? command, GridPosition? cell)
  {
    if (cell is { } position)
    {
      Execute(command, position);
    }
  }

  private void SetHover(GridPosition? cell)
  {
    if (_hover != cell)
    {
      _hover = cell;
      InvalidateVisual();
    }
  }

  private void OnSourceChanged(DependencyPropertyChangedEventArgs e)
  {
    if (!IsLoaded)
    {
      return;
    }

    if (e.Property == GameProperty)
    {
      Detach(e.OldValue as InfiniteGame, null);
      Attach(e.NewValue as InfiniteGame, null);
    }
    else
    {
      Detach(null, e.OldValue as Camera);
      Attach(null, e.NewValue as Camera);
    }

    InvalidateVisual();
  }

  private void Attach(InfiniteGame? game, Camera? camera)
  {
    if (game is not null)
    {
      game.Changed += InvalidateVisual;
    }

    if (camera is not null)
    {
      camera.PropertyChanged += OnCameraChanged;
    }
  }

  private void Detach(InfiniteGame? game, Camera? camera)
  {
    if (game is not null)
    {
      game.Changed -= InvalidateVisual;
    }

    if (camera is not null)
    {
      camera.PropertyChanged -= OnCameraChanged;
    }
  }

  private void OnCameraChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

  // Cores do tema do HandyControl copiadas em pincéis próprios e congelados: os do tema têm a cor ligada a
  // outro recurso e não congelam, e pincel congelado desenha mais rápido.
  private void LoadResources()
  {
    Brush Find(string key, Color fallback, double opacity = 1)
    {
      var color = TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;
      var copy = new SolidColorBrush(color) { Opacity = opacity };
      copy.Freeze();
      return copy;
    }

    _hidden = Find("SecondaryRegionBrush", Colors.LightGray);
    _hiddenHover = Find("BorderBrush", Colors.Gray);
    _open = Find("RegionBrush", Colors.White);
    _flag = Find("DangerBrush", Colors.Red);
    _danger = Find("DangerBrush", Colors.Red);
    _solvedMap = Find("SuccessBrush", Colors.Green, 0.45);
    _lockedMap = Find("DangerBrush", Colors.Red, 0.45);
    _solvedOverlay = Find("SuccessBrush", Colors.Green, 0.16);
    _lockedOverlay = Find("DangerBrush", Colors.Red, 0.16);
    _rangeFill = Find("LightWarningBrush", Colors.LightYellow);
    _rangePen = new Pen(Find("WarningBrush", Colors.Orange), 2);
    _rangePen.Freeze();
    _sectorLine = Find("SecondaryTextBrush", Colors.Gray, 0.35);
    _flagGeometry = TryFindResource("FlagGeometry") as Geometry ?? Geometry.Empty;
    _lockGeometry = TryFindResource("LockGeometry") as Geometry ?? Geometry.Empty;
    _bomb = TryFindResource("BombEmoji") as ImageSource;
  }

  // Uma célula fechada por ladrilho, alinhado com a grade: desenha milhares de fechadas num retângulo só.
  private Brush HiddenTiles(Viewport view)
  {
    var size = view.Size;
    var gap = Math.Max(1, size * 0.08);
    var radius = size * 0.16;

    var drawing = new DrawingGroup();
    using (var context = drawing.Open())
    {
      // Retângulo transparente do tamanho da célula: é ele que define o tamanho do ladrilho.
      context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));
      context.DrawRoundedRectangle(_hidden, null, new Rect(gap / 2, gap / 2, size - gap, size - gap), radius, radius);
    }

    var brush = new DrawingBrush(drawing)
    {
      TileMode = TileMode.Tile,
      Stretch = Stretch.None,
      ViewboxUnits = BrushMappingMode.Absolute,
      Viewbox = new Rect(0, 0, size, size),
      ViewportUnits = BrushMappingMode.Absolute,
      Viewport = new Rect(view.ScreenX(view.FirstX), view.ScreenY(view.FirstY), size, size),
    };

    brush.Freeze();
    return brush;
  }

  private static IEnumerable<Sector> VisibleSectors(InfiniteGame game, Viewport view)
  {
    for (var sy = view.FirstY >> 3; sy <= view.LastY >> 3; sy++)
    {
      for (var sx = view.FirstX >> 3; sx <= view.LastX >> 3; sx++)
      {
        if (game.FindSector(new SectorPosition(sx, sy)) is { } sector)
        {
          yield return sector;
        }
      }
    }
  }

  private void DrawSectorCells(DrawingContext dc, InfiniteGame game, Sector sector, Viewport view)
  {
    var size = view.Size;
    var baseX = sector.Position.X * Sector.Size;
    var baseY = sector.Position.Y * Sector.Size;
    var showDetail = size >= DetailThreshold;

    for (var ly = 0; ly < Sector.Size; ly++)
    {
      var y = baseY + ly;
      if (y < view.FirstY || y > view.LastY)
      {
        continue;
      }

      var top = view.ScreenY(y);

      // Abertas vizinhas na mesma linha viram um retângulo só. Meio pixel a mais de cada lado tapa a emenda do antialias.
      var runStart = -1;
      for (var lx = 0; lx <= Sector.Size; lx++)
      {
        var isOpen = lx < Sector.Size && game.GetCell(new GridPosition(baseX + lx, y)) is { State: CellState.Revealed, IsExploded: false };

        if (isOpen && runStart < 0)
        {
          runStart = lx;
        }
        else if (!isOpen && runStart >= 0)
        {
          var left = view.ScreenX(baseX + runStart);
          dc.DrawRectangle(_open, null, new Rect(left - 0.5, top - 0.5, ((lx - runStart) * size) + 1, size + 1));
          runStart = -1;
        }
      }

      for (var lx = 0; lx < Sector.Size; lx++)
      {
        var x = baseX + lx;
        if (x < view.FirstX || x > view.LastX)
        {
          continue;
        }

        var cell = game.GetCell(new GridPosition(x, y));
        var rect = new Rect(view.ScreenX(x), top, size, size);

        if (cell.IsExploded)
        {
          var inset = Math.Max(1, size * 0.04);
          var radius = size * 0.16;
          dc.DrawRoundedRectangle(_danger, null, Rect.Inflate(rect, -inset, -inset), radius, radius);
          DrawBomb(dc, rect, showDetail);
        }
        else if (cell.State == CellState.Flagged)
        {
          DrawIcon(dc, _flagGeometry, _flag, rect, 0.5, showDetail);
        }
        else if (cell.IsMine)
        {
          DrawBomb(dc, rect, showDetail);
        }
        else if (showDetail && cell is { State: CellState.Revealed, AdjacentMines: > 0 } number)
        {
          DrawNumber(dc, number.AdjacentMines, rect);
        }
      }
    }
  }

  private void DrawSectorOverlay(DrawingContext dc, Sector sector, Viewport view)
  {
    if (sector.State == SectorState.Open)
    {
      return;
    }

    var span = view.Size * Sector.Size;
    var rect = new Rect(view.ScreenX(sector.Position.X * Sector.Size), view.ScreenY(sector.Position.Y * Sector.Size), span, span);

    if (sector.State == SectorState.Solved)
    {
      dc.DrawRectangle(_solvedOverlay, null, rect);
      return;
    }

    dc.DrawRectangle(_lockedOverlay, null, rect);

    // Cadeado no meio pra dizer que não adianta clicar.
    var iconSize = Math.Min(span * 0.4, 56);
    if (iconSize >= 10)
    {
      var iconRect = new Rect(rect.X + ((span - iconSize) / 2), rect.Y + ((span - iconSize) / 2), iconSize, iconSize);
      DrawGeometry(dc, _lockGeometry, _danger, iconRect);
    }
  }

  // Linhas a cada 8 células marcam os setores.
  private void DrawSectorLines(DrawingContext dc, Viewport view, Rect bounds)
  {
    var thickness = Math.Max(1, view.Size * 0.06);
    var firstX = (view.FirstX >> 3) << 3;
    var firstY = (view.FirstY >> 3) << 3;

    for (var x = firstX; x <= view.LastX + 1; x += Sector.Size)
    {
      dc.DrawRectangle(_sectorLine, null, new Rect(view.ScreenX(x) - (thickness / 2), 0, thickness, bounds.Height));
    }

    for (var y = firstY; y <= view.LastY + 1; y += Sector.Size)
    {
      dc.DrawRectangle(_sectorLine, null, new Rect(0, view.ScreenY(y) - (thickness / 2), bounds.Width, thickness));
    }
  }

  // Fechada sob o mouse escurece; número sob o mouse acende as vizinhas fechadas (onde a bomba pode estar).
  private void DrawHover(DrawingContext dc, InfiniteGame game, Viewport view)
  {
    if (_hover is not { } hover || game.IsOver || IsLocked(game, hover))
    {
      return;
    }

    var size = view.Size;
    var gap = Math.Max(1, size * 0.08);
    var radius = size * 0.16;
    var cell = game.GetCell(hover);

    Rect TileRect(GridPosition position) =>
      new(view.ScreenX(position.X) + (gap / 2), view.ScreenY(position.Y) + (gap / 2), size - gap, size - gap);

    if (cell.State == CellState.Hidden)
    {
      dc.DrawRoundedRectangle(_hiddenHover, null, TileRect(hover), radius, radius);
      return;
    }

    if (cell is not { State: CellState.Revealed, AdjacentMines: > 0 })
    {
      return;
    }

    for (var dy = -1; dy <= 1; dy++)
    {
      for (var dx = -1; dx <= 1; dx++)
      {
        var neighbor = new GridPosition(hover.X + dx, hover.Y + dy);
        if ((dx != 0 || dy != 0) && game.GetCell(neighbor).State == CellState.Hidden && !IsLocked(game, neighbor))
        {
          dc.DrawRoundedRectangle(_rangeFill, _rangePen, TileRect(neighbor), radius, radius);
        }
      }
    }

    dc.DrawRoundedRectangle(null, _rangePen, TileRect(hover), radius, radius);
  }

  private static bool IsLocked(InfiniteGame game, GridPosition cell) =>
    game.FindSector(InfiniteGame.SectorOf(cell))?.State == SectorState.Locked;

  // Zoom bem afastado: cada setor vira um bloco colorido pelo estado, como um mapa.
  private void DrawMap(DrawingContext dc, InfiniteGame game, Viewport view, Rect bounds)
  {
    dc.DrawRectangle(_hidden, null, bounds);

    var span = view.Size * Sector.Size;

    foreach (var sector in VisibleSectors(game, view))
    {
      var rect = new Rect(view.ScreenX(sector.Position.X * Sector.Size), view.ScreenY(sector.Position.Y * Sector.Size), span, span);

      switch (sector.State)
      {
        case SectorState.Solved:
          dc.DrawRectangle(_solvedMap, null, rect);
          break;

        case SectorState.Locked:
          dc.DrawRectangle(_lockedMap, null, rect);
          break;

        // Setor criado só pra contar minas da borda não tem nada aberto: fica com cara de fechado.
        case SectorState.Open when sector.RevealedSafeCount > 0:
          dc.PushOpacity(0.25 + (0.75 * sector.RevealedSafeCount / sector.SafeCount));
          dc.DrawRectangle(_open, null, rect);
          dc.Pop();
          break;
      }
    }

    if (span >= 12)
    {
      DrawSectorLines(dc, view, bounds);
    }
  }

  private void DrawBomb(DrawingContext dc, Rect cell, bool showDetail)
  {
    if (_bomb is null || !showDetail)
    {
      return;
    }

    var size = cell.Width * 0.6;
    dc.DrawImage(_bomb, new Rect(cell.X + ((cell.Width - size) / 2), cell.Y + ((cell.Height - size) / 2), size, size));
  }

  // Sem espaço pro desenho, um quadradinho na cor do ícone já diz o que é.
  private static void DrawIcon(DrawingContext dc, Geometry geometry, Brush brush, Rect cell, double scale, bool showDetail)
  {
    var size = cell.Width * scale;
    var rect = new Rect(cell.X + ((cell.Width - size) / 2), cell.Y + ((cell.Height - size) / 2), size, size);

    if (showDetail)
    {
      DrawGeometry(dc, geometry, brush, rect);
    }
    else
    {
      dc.DrawRectangle(brush, null, rect);
    }
  }

  // Os ícones do Icons.xaml são 256x256.
  private static void DrawGeometry(DrawingContext dc, Geometry geometry, Brush brush, Rect rect)
  {
    var scale = rect.Width / 256;
    dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, rect.X, rect.Y));
    dc.DrawGeometry(brush, null, geometry);
    dc.Pop();
  }

  private void DrawNumber(DrawingContext dc, int number, Rect cell)
  {
    var text = NumberText(number, Math.Round(cell.Width * 0.55));
    dc.DrawText(text, new Point(cell.X + ((cell.Width - text.Width) / 2), cell.Y + ((cell.Height - text.Height) / 2)));
  }

  // FormattedText é caro de criar: guarda um por número enquanto o zoom não muda.
  private FormattedText NumberText(int number, double fontSize)
  {
    if (fontSize != _numbersFontSize)
    {
      _numbers.Clear();
      _numbersFontSize = fontSize;
    }

    if (!_numbers.TryGetValue(number, out var text))
    {
      text = new FormattedText(
        number.ToString(CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
        fontSize,
        NumberToBrushConverter.BrushFor(number) ?? Brushes.Black,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

      _numbers[number] = text;
    }

    return text;
  }

  // Faixa de células visíveis e conversão de célula pra pixel na tela.
  private readonly struct Viewport
  {
    private readonly double _left;
    private readonly double _top;

    public Viewport(Camera camera, Size size)
    {
      Size = camera.CellSize;
      _left = camera.CenterX - (size.Width / 2 / Size);
      _top = camera.CenterY - (size.Height / 2 / Size);

      FirstX = (int)Math.Floor(_left);
      FirstY = (int)Math.Floor(_top);
      LastX = (int)Math.Floor(_left + (size.Width / Size));
      LastY = (int)Math.Floor(_top + (size.Height / Size));
    }

    public double Size { get; }

    public int FirstX { get; }

    public int FirstY { get; }

    public int LastX { get; }

    public int LastY { get; }

    public double ScreenX(int x) => (x - _left) * Size;

    public double ScreenY(int y) => (y - _top) * Size;
  }
}
