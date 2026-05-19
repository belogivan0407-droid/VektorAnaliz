using System;
using System.Windows;
using System.Windows.Media;
using System.Numerics;
using HelixToolkit.Wpf;
using NCalc; // Требуется пакет NCalcCore

// Создаем четкие псевдонимы
using WpfMedia = System.Windows.Media.Media3D;
using HelixGeo = HelixToolkit.Geometry;

namespace Vektoranaliz
{
    public partial class MainWindow : Window
    {
        private WpfMedia.MeshGeometry3D currentMesh = new WpfMedia.MeshGeometry3D();
        private WpfMedia.ModelVisual3D currentVectorMarker = null; // Для хранения и удаления текущей стрелки при наведении

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---
        private Vector3 V3(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);

        private WpfMedia.MeshGeometry3D ConvertMesh(HelixGeo.MeshGeometry3D hMesh)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            if (hMesh.Positions != null)
                foreach (var p in hMesh.Positions) wMesh.Positions.Add(new WpfMedia.Point3D(p.X, p.Y, p.Z));
            if (hMesh.TriangleIndices != null)
                foreach (var idx in hMesh.TriangleIndices) wMesh.TriangleIndices.Add(idx);
            return wMesh;
        }

        // --- ПАРСИНГ ВЕКТОРНОГО ПОЛЯ ---
        private WpfMedia.Vector3D EvaluateVectorField(double x, double y, double z)
        {
            try
            {
                return new WpfMedia.Vector3D(
                    EvaluateExpression(TextBoxAx.Text, x, y, z),
                    EvaluateExpression(TextBoxAy.Text, x, y, z),
                    EvaluateExpression(TextBoxAz.Text, x, y, z)
                );
            }
            catch
            {
                return new WpfMedia.Vector3D(0, 0, 0); // Если формула неверная, возвращаем 0
            }
        }

        private double EvaluateExpression(string expression, double x, double y, double z)
        {
            // Явно указываем NCalc.Expression, чтобы избежать конфликта с System.Windows
            var expr = new NCalc.Expression(expression);
            expr.Parameters["x"] = x;
            expr.Parameters["y"] = y;
            expr.Parameters["z"] = z;

            return Convert.ToDouble(expr.Evaluate());
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

        private void AddParaboloid_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int resolution = 40;
            double maxRadius = 2.0;

            // Генерация вершин
            for (int i = 0; i <= resolution; i++)
            {
                double r = maxRadius * i / resolution;
                for (int j = 0; j <= resolution; j++)
                {
                    double theta = 2 * Math.PI * j / resolution;
                    double x = r * Math.Cos(theta);
                    double y = r * Math.Sin(theta);
                    double z = x * x + y * y;
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }

            // Индексы треугольников
            for (int i = 0; i < resolution; i++)
            {
                for (int j = 0; j < resolution; j++)
                {
                    int p1 = i * (resolution + 1) + j;
                    int p2 = p1 + 1;
                    int p3 = (i + 1) * (resolution + 1) + j;
                    int p4 = p3 + 1;

                    wMesh.TriangleIndices.Add(p1);
                    wMesh.TriangleIndices.Add(p3);
                    wMesh.TriangleIndices.Add(p2);

                    wMesh.TriangleIndices.Add(p2);
                    wMesh.TriangleIndices.Add(p3);
                    wMesh.TriangleIndices.Add(p4);
                }
            }
            SetupNewSurface(wMesh);
        }

        private void SetupNewSurface(WpfMedia.MeshGeometry3D mesh)
        {
            currentMesh = mesh;
            ModelContainer.Children.Clear();
            currentVectorMarker = null; // Сбрасываем старый вектор

            var material = MaterialHelper.CreateMaterial(Colors.DodgerBlue, 0.6);
            var model = new WpfMedia.GeometryModel3D(mesh, material) { BackMaterial = material };

            ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = model });

            // После отрисовки сразу считаем поток
            CalculateFlux(mesh);
            Viewport.ZoomExtents();
        }

        // --- ЗАДАЧА 2 и 3: Отрисовка вектора при наведении ---
        private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (currentMesh.Positions.Count == 0) return;

            Point mousePos = e.GetPosition(Viewport);
            bool isHit = Viewport3DHelper.FindNearest(Viewport.Viewport, mousePos, out WpfMedia.Point3D hitPoint, out WpfMedia.Vector3D normal, out DependencyObject visual);

            if (isHit && visual != currentVectorMarker)
            {
                DrawFieldVector(hitPoint);
            }
        }

        private void DrawFieldVector(WpfMedia.Point3D p)
        {
            // Удаляем старый вектор с экрана
            if (currentVectorMarker != null)
            {
                ModelContainer.Children.Remove(currentVectorMarker);
            }

            var fieldA = EvaluateVectorField(p.X, p.Y, p.Z);

            var mb = new HelixGeo.MeshBuilder();
            // Масштабируем отрисовываемый вектор для наглядности (* 0.3)
            var endPoint = V3(p.X + fieldA.X * 0.3, p.Y + fieldA.Y * 0.3, p.Z + fieldA.Z * 0.3);
            mb.AddArrow(V3(p.X, p.Y, p.Z), endPoint, 0.05f);

            var vectorModel = new WpfMedia.GeometryModel3D(ConvertMesh(mb.ToMesh()), Materials.Red);
            currentVectorMarker = new WpfMedia.ModelVisual3D { Content = vectorModel };
            ModelContainer.Children.Add(currentVectorMarker);
        }

        // --- ЗАДАЧА 4: Расчет потока ---
        private void CalculateFlux(WpfMedia.MeshGeometry3D mesh)
        {
            double totalFlux = 0;
            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
            {
                var p1 = mesh.Positions[mesh.TriangleIndices[i]];
                var p2 = mesh.Positions[mesh.TriangleIndices[i + 1]];
                var p3 = mesh.Positions[mesh.TriangleIndices[i + 2]];

                // Центр треугольника
                double cx = (p1.X + p2.X + p3.X) / 3.0;
                double cy = (p1.Y + p2.Y + p3.Y) / 3.0;
                double cz = (p1.Z + p2.Z + p3.Z) / 3.0;

                // Значение вектора в центре треугольника
                var fieldA = EvaluateVectorField(cx, cy, cz);

                // Нормаль и площадь
                double v1x = p2.X - p1.X; double v1y = p2.Y - p1.Y; double v1z = p2.Z - p1.Z;
                double v2x = p3.X - p1.X; double v2y = p3.Y - p1.Y; double v2z = p3.Z - p1.Z;

                double dSx = (v1y * v2z - v1z * v2y) * 0.5;
                double dSy = (v1z * v2x - v1x * v2z) * 0.5;
                double dSz = (v1x * v2y - v1y * v2x) * 0.5;

                // Интегрирование (Скалярное произведение)
                totalFlux += (fieldA.X * dSx + fieldA.Y * dSy + fieldA.Z * dSz);
            }
            FluxResult.Text = $"Ф = {totalFlux:F4}";
        }
    }
}