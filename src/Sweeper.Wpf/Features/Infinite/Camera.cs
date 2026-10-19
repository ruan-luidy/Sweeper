using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sweeper.Wpf.Features.Infinite;

// Onde a tela está olhando no mapa. Centro em coordenadas de célula (0.5 = meio da célula 0) e tamanho da célula em pixels.
public sealed partial class Camera : ObservableObject
{
  public const double MinCellSize = 3;
  public const double MaxCellSize = 72;
  public const double DefaultCellSize = 32;

  [ObservableProperty]
  public partial double CenterX { get; set; } = 0.5;

  [ObservableProperty]
  public partial double CenterY { get; set; } = 0.5;

  [ObservableProperty]
  public partial double CellSize { get; set; } = DefaultCellSize;

  public Point ScreenToWorld(Point screen, Size viewport) => new(
    CenterX + ((screen.X - (viewport.Width / 2)) / CellSize),
    CenterY + ((screen.Y - (viewport.Height / 2)) / CellSize));

  // Arrastar o mapa: ele anda junto com o mouse.
  public void PanBy(Vector screenDelta)
  {
    CenterX -= screenDelta.X / CellSize;
    CenterY -= screenDelta.Y / CellSize;
  }

  // Zoom mantendo parado o ponto do mapa que está sob o mouse.
  public void ZoomAt(double factor, Point screen, Size viewport)
  {
    var before = ScreenToWorld(screen, viewport);
    CellSize = Math.Clamp(CellSize * factor, MinCellSize, MaxCellSize);
    var after = ScreenToWorld(screen, viewport);

    CenterX += before.X - after.X;
    CenterY += before.Y - after.Y;
  }

  // Pros botões, que não têm posição de mouse: zoom no centro.
  public void ZoomBy(double factor) =>
    CellSize = Math.Clamp(CellSize * factor, MinCellSize, MaxCellSize);

  public void CenterOnCell(int x, int y)
  {
    CenterX = x + 0.5;
    CenterY = y + 0.5;
  }

  public void Reset()
  {
    CenterOnCell(0, 0);
    CellSize = DefaultCellSize;
  }
}
