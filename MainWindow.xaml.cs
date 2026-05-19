using System;
using System.Windows;
using System.Windows.Media;
using System.Numerics;
using HelixToolkit.Wpf;

// Создаем четкие псевдонимы, чтобы компилятор не путался (убирает CS0104)
using WpfMedia = System.Windows.Media.Media3D;
using HelixGeo = HelixToolkit.Geometry;

namespace Vektoranaliz
{
    public partial class MainWindow : Window
    {
        private WpfMedia.MeshGeometry3D currentMesh = new WpfMedia.MeshGeometry3D();

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (Убирают ошибки CS1503) ---

        // 1. Быстрое создание вектора для Helix (double -> float)
        private Vector3 V3(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);

        // 2. Ручной конвертер сетки из Helix в WPF (работает на ЛЮБОЙ версии)
        private WpfMedia.MeshGeometry3D ConvertMesh(HelixGeo.MeshGeometry3D hMesh)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            if (hMesh.Positions != null)
                foreach (var p in hMesh.Positions) wMesh.Positions.Add(new WpfMedia.Point3D(p.X, p.Y, p.Z));
            if (hMesh.TriangleIndices != null)
                foreach (var idx in hMesh.TriangleIndices) wMesh.TriangleIndices.Add(idx);
            return wMesh;
        }

        // --- ЗАДАЧА 1: Построение поверхностей ---

        private void AddSphere_Click(object sender, RoutedEventArgs e)
        {
            var mb = new HelixGeo.MeshBuilder();
            mb.AddSphere(V3(0, 0, 0), 2.0f);
            SetupNewSurface(ConvertMesh(mb.ToMesh()));
        }

        private void AddCylinder_Click(object sender, RoutedEventArgs e)
        {
            var mb = new HelixGeo.MeshBuilder();
            mb.AddCylinder(V3(0, 0, -2), V3(0, 0, 2), 1.5f, 40);
            SetupNewSurface(ConvertMesh(mb.ToMesh()));
        }

        private void SetupNewSurface(WpfMedia.MeshGeometry3D mesh)
        {
            currentMesh = mesh;
            ModelContainer.Children.Clear();

            var material = MaterialHelper.CreateMaterial(Colors.DodgerBlue, 0.6);
            var model = new WpfMedia.GeometryModel3D(mesh, material) { BackMaterial = material };

            ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = model });
            CalculateFlux(mesh);
            Viewport.ZoomExtents();
        }

        // --- ЗАДАЧА 2: Отрисовка вектора ---

        private void Viewport_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point mousePos = e.GetPosition(Viewport);

            // Правильный вызов FindNearest (убирает CS7036, CS1061, CS0472)
            bool isHit = Viewport3DHelper.FindNearest(Viewport.Viewport, mousePos, out WpfMedia.Point3D hitPoint, out WpfMedia.Vector3D normal, out DependencyObject visual);

            if (isHit)
            {
                DrawFieldVector(hitPoint);
            }
        }

        private void DrawFieldVector(WpfMedia.Point3D p)
        {
            double r2 = p.X * p.X + p.Y * p.Y + p.Z * p.Z;
            double r = Math.Sqrt(r2);
            if (r < 0.1) return;

            // Формула поля: a = r / |r|^3
            double ax = p.X / (r2 * r);
            double ay = p.Y / (r2 * r);
            double az = p.Z / (r2 * r);

            var mb = new HelixGeo.MeshBuilder();
            mb.AddArrow(V3(p.X, p.Y, p.Z), V3(p.X + ax * 3, p.Y + ay * 3, p.Z + az * 3), 0.06f);

            var vectorModel = new WpfMedia.GeometryModel3D(ConvertMesh(mb.ToMesh()), Materials.Red);
            ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = vectorModel });
        }

        // --- ЗАДАЧА 3: Расчет потока ---

        private void CalculateFlux(WpfMedia.MeshGeometry3D mesh)
        {
            double totalFlux = 0;
            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
            {
                var p1 = mesh.Positions[mesh.TriangleIndices[i]];
                var p2 = mesh.Positions[mesh.TriangleIndices[i + 1]];
                var p3 = mesh.Positions[mesh.TriangleIndices[i + 2]];

                // 1. Центр треугольника
                double cx = (p1.X + p2.X + p3.X) / 3.0;
                double cy = (p1.Y + p2.Y + p3.Y) / 3.0;
                double cz = (p1.Z + p2.Z + p3.Z) / 3.0;

                // 2. Поле в центре
                double r2 = cx * cx + cy * cy + cz * cz;
                double r = Math.Sqrt(r2);
                double ax = cx / (r2 * r);
                double ay = cy / (r2 * r);
                double az = cz / (r2 * r);

                // 3. Векторы сторон для площади
                double v1x = p2.X - p1.X; double v1y = p2.Y - p1.Y; double v1z = p2.Z - p1.Z;
                double v2x = p3.X - p1.X; double v2y = p3.Y - p1.Y; double v2z = p3.Z - p1.Z;

                // 4. Вектор площади (Векторное произведение)
                double dSx = (v1y * v2z - v1z * v2y) * 0.5;
                double dSy = (v1z * v2x - v1x * v2z) * 0.5;
                double dSz = (v1x * v2y - v1y * v2x) * 0.5;

                // 5. Поток dФ = a * dS
                totalFlux += (ax * dSx + ay * dSy + az * dSz);
            }
            FluxResult.Text = $"Ф = {Math.Abs(totalFlux):F3}";
        }
    }
}