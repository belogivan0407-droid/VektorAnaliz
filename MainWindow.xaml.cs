using System;
using System.Windows;
using System.Windows.Media;
using System.Numerics;
using HelixToolkit.Wpf;
using NCalc;

// Создаем четкие псевдонимы
using WpfMedia = System.Windows.Media.Media3D;
using HelixGeo = HelixToolkit.Geometry;

namespace Vektoranaliz
{
    public partial class MainWindow : Window
    {
        private WpfMedia.MeshGeometry3D currentMesh = new WpfMedia.MeshGeometry3D();
        private WpfMedia.ModelVisual3D currentVectorMarker = null;
        private WpfMedia.ModelVisual3D currentContourMarker = null;

        // Явное указание NCalc.Expression исключает конфликт со стандартным System.Windows.Expression
        private NCalc.Expression exprX, exprY, exprZ;
        private bool isFieldValid = false;

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

        // Универсальный генератор индексов сетки с поддержкой изменения направления обхода (для ориентации нормалей)
        private void BuildGridIndices(WpfMedia.MeshGeometry3D wMesh, int uRes, int vRes, bool reverseWinding = false, int offset = 0)
        {
            for (int i = 0; i < uRes; i++)
            {
                for (int j = 0; j < vRes; j++)
                {
                    int p1 = offset + i * (vRes + 1) + j;
                    int p2 = p1 + 1;
                    int p3 = offset + (i + 1) * (vRes + 1) + j;
                    int p4 = p3 + 1;

                    if (reverseWinding)
                    {
                        // Обход по часовой стрелке
                        wMesh.TriangleIndices.Add(p1);
                        wMesh.TriangleIndices.Add(p3);
                        wMesh.TriangleIndices.Add(p2);

                        wMesh.TriangleIndices.Add(p2);
                        wMesh.TriangleIndices.Add(p3);
                        wMesh.TriangleIndices.Add(p4);
                    }
                    else
                    {
                        // Обход против часовой стрелки
                        wMesh.TriangleIndices.Add(p1);
                        wMesh.TriangleIndices.Add(p2);
                        wMesh.TriangleIndices.Add(p3);

                        wMesh.TriangleIndices.Add(p2);
                        wMesh.TriangleIndices.Add(p4);
                        wMesh.TriangleIndices.Add(p3);
                    }
                }
            }
        }

        // --- ПАРСИНГ ВЕКТОРНОГО ПОЛЯ ---
        private bool UpdateVectorFieldExpressions()
        {
            try
            {
                exprX = new NCalc.Expression(TextBoxAx.Text);
                exprY = new NCalc.Expression(TextBoxAy.Text);
                exprZ = new NCalc.Expression(TextBoxAz.Text);

                isFieldValid = true;
                EvaluateVectorField(0, 0, 0); // Тестовый расчет для валидации формулы

                ErrorText.Text = "";
                return true;
            }
            catch
            {
                ErrorText.Text = "Ошибка в формуле поля! Проверьте синтаксис (например: x * y, а не xy)";
                FluxResult.Text = "Ф = ---";
                isFieldValid = false;
                return false;
            }
        }

        private WpfMedia.Vector3D EvaluateVectorField(double x, double y, double z)
        {
            if (!isFieldValid) return new WpfMedia.Vector3D(0, 0, 0);

            try
            {
                exprX.Parameters["x"] = x; exprX.Parameters["y"] = y; exprX.Parameters["z"] = z;
                exprY.Parameters["x"] = x; exprY.Parameters["y"] = y; exprY.Parameters["z"] = z;
                exprZ.Parameters["x"] = x; exprZ.Parameters["y"] = y; exprZ.Parameters["z"] = z;

                return new WpfMedia.Vector3D(
                    Convert.ToDouble(exprX.Evaluate()),
                    Convert.ToDouble(exprY.Evaluate()),
                    Convert.ToDouble(exprZ.Evaluate())
                );
            }
            catch
            {
                return new WpfMedia.Vector3D(0, 0, 0);
            }
        }

        // --- ГЕНЕРАЦИЯ 9 КВАДРИК ВТОРОГО ПОРЯДКА ---

        // 1. Эллипсоид (Обобщенная сфера)
        private void AddEllipsoid_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 40, vRes = 40;
            double a = 2.0, b = 1.5, c = 1.2;
            for (int i = 0; i <= uRes; i++)
            {
                double phi = Math.PI * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double theta = 2 * Math.PI * j / vRes;
                    double x = a * Math.Sin(phi) * Math.Cos(theta);
                    double y = b * Math.Sin(phi) * Math.Sin(theta);
                    double z = c * Math.Cos(phi);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, true); // Разворачиваем нормали наружу
            SetupNewSurface(wMesh);
        }

        // 2. Однополостный гиперболоид
        private void AddOneSheetHyperboloid_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 40, vRes = 40;
            double a = 1.0, b = 1.0, c = 1.2;
            for (int i = 0; i <= uRes; i++)
            {
                double u = -1.2 + 2.4 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double v = 2 * Math.PI * j / vRes;
                    double x = a * Math.Cosh(u) * Math.Cos(v);
                    double y = b * Math.Cosh(u) * Math.Sin(v);
                    double z = c * Math.Sinh(u);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, false);
            SetupNewSurface(wMesh);
        }

        // 3. Двуполостный гиперболоид (Обе полости)
        private void AddTwoSheetHyperboloid_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 20, vRes = 40;
            double a = 1.0, b = 1.0, c = 1.0;

            // Верхняя полость (z >= c)
            for (int i = 0; i <= uRes; i++)
            {
                double u = 0.0 + 1.3 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double v = 2 * Math.PI * j / vRes;
                    double x = a * Math.Sinh(u) * Math.Cos(v);
                    double y = b * Math.Sinh(u) * Math.Sin(v);
                    double z = c * Math.Cosh(u);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, false);

            int offset = wMesh.Positions.Count;

            // Нижняя полость (z <= -c)
            for (int i = 0; i <= uRes; i++)
            {
                double u = 0.0 + 1.3 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double v = 2 * Math.PI * j / vRes;
                    double x = a * Math.Sinh(u) * Math.Cos(v);
                    double y = b * Math.Sinh(u) * Math.Sin(v);
                    double z = -c * Math.Cosh(u);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, true, offset); // Разворачиваем нижнюю полость
            SetupNewSurface(wMesh);
        }

        // 4. Эллиптический конус
        private void AddEllipticCone_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 40, vRes = 40;
            double a = 1.0, b = 1.0, c = 1.0;
            for (int i = 0; i <= uRes; i++)
            {
                double u = -1.5 + 3.0 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double v = 2 * Math.PI * j / vRes;
                    double x = a * u * Math.Cos(v);
                    double y = b * u * Math.Sin(v);
                    double z = c * u;
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, false);
            SetupNewSurface(wMesh);
        }

        // 5. Эллиптический параболоид
        private void AddParaboloid_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int resolution = 40;
            double maxRadius = 2.0;

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
            BuildGridIndices(wMesh, resolution, resolution, false);
            SetupNewSurface(wMesh);
        }

        // 6. Гиперболический параболоид (Седло)
        private void AddHyperbolicParaboloid_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 40, vRes = 40;
            double a = 1.0, b = 1.0;
            for (int i = 0; i <= uRes; i++)
            {
                double x = -1.5 + 3.0 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double y = -1.5 + 3.0 * j / vRes;
                    double z = (x * x) / (a * a) - (y * y) / (b * b);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, false);
            SetupNewSurface(wMesh);
        }

        // 7. Эллиптический цилиндр
        private void AddEllipticCylinder_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 40, vRes = 40;
            double a = 1.5, b = 1.0;
            for (int i = 0; i <= uRes; i++)
            {
                double z = -2.0 + 4.0 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double theta = 2 * Math.PI * j / vRes;
                    double x = a * Math.Cos(theta);
                    double y = b * Math.Sin(theta);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, false);
            SetupNewSurface(wMesh);
        }

        // 8. Гиперболический цилиндр
        private void AddHyperbolicCylinder_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 20, vRes = 20;
            double a = 1.0, b = 1.0;

            // Первая ветвь (x > 0)
            for (int i = 0; i <= uRes; i++)
            {
                double u = -1.2 + 2.4 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double z = -2.0 + 4.0 * j / vRes;
                    double x = a * Math.Cosh(u);
                    double y = b * Math.Sinh(u);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, true);

            int offset = wMesh.Positions.Count;

            // Вторая ветвь (x < 0)
            for (int i = 0; i <= uRes; i++)
            {
                double u = -1.2 + 2.4 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double z = -2.0 + 4.0 * j / vRes;
                    double x = -a * Math.Cosh(u);
                    double y = b * Math.Sinh(u);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, false, offset);
            SetupNewSurface(wMesh);
        }

        // 9. Параболический цилиндр
        private void AddParabolicCylinder_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int uRes = 40, vRes = 40;
            double p = 1.0;
            for (int i = 0; i <= uRes; i++)
            {
                double y = -1.5 + 3.0 * i / uRes;
                for (int j = 0; j <= vRes; j++)
                {
                    double z = -2.0 + 4.0 * j / vRes;
                    double x = (y * y) / (2.0 * p);
                    wMesh.Positions.Add(new WpfMedia.Point3D(x, y, z));
                }
            }
            BuildGridIndices(wMesh, uRes, vRes, true);
            SetupNewSurface(wMesh);
        }

        // --- ОСОБЫЙ РЕЖИМ: Пересечение и контур ---
        private void AddIntersection_Click(object sender, RoutedEventArgs e)
        {
            var wMesh = new WpfMedia.MeshGeometry3D();
            int resolution = 40;
            double maxRadius = 2.0;
            double planeZ = 4.0;

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

            BuildGridIndices(wMesh, resolution, resolution, false);
            SetupNewSurface(wMesh);
            DrawContour(maxRadius, planeZ, resolution);

            // Отрисовка полупрозрачной плоскости сечения
            var planeMesh = new WpfMedia.MeshGeometry3D();
            planeMesh.Positions.Add(new WpfMedia.Point3D(-maxRadius, -maxRadius, planeZ));
            planeMesh.Positions.Add(new WpfMedia.Point3D(maxRadius, -maxRadius, planeZ));
            planeMesh.Positions.Add(new WpfMedia.Point3D(maxRadius, maxRadius, planeZ));
            planeMesh.Positions.Add(new WpfMedia.Point3D(-maxRadius, maxRadius, planeZ));

            planeMesh.TriangleIndices.Add(0); planeMesh.TriangleIndices.Add(1); planeMesh.TriangleIndices.Add(2);
            planeMesh.TriangleIndices.Add(0); planeMesh.TriangleIndices.Add(2); planeMesh.TriangleIndices.Add(3);

            var planeMat = MaterialHelper.CreateMaterial(Colors.Green, 0.4);
            var planeModel = new WpfMedia.GeometryModel3D(planeMesh, planeMat) { BackMaterial = planeMat };
            ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = planeModel });
        }

        private void DrawContour(double radius, double zHeight, int resolution)
        {
            var mb = new HelixGeo.MeshBuilder();
            var path = new System.Collections.Generic.List<Vector3>();

            for (int j = 0; j <= resolution; j++)
            {
                double theta = 2 * Math.PI * j / resolution;
                double x = radius * Math.Cos(theta);
                double y = radius * Math.Sin(theta);
                path.Add(V3(x, y, zHeight));
            }

            mb.AddTube(path, 0.05f, 12, false);
            var material = MaterialHelper.CreateMaterial(Colors.Yellow, 1.0);
            var model = new WpfMedia.GeometryModel3D(ConvertMesh(mb.ToMesh()), material);

            currentContourMarker = new WpfMedia.ModelVisual3D { Content = model };
            ModelContainer.Children.Add(currentContourMarker);
        }

        private void SetupNewSurface(WpfMedia.MeshGeometry3D mesh)
        {
            currentMesh = mesh;
            ModelContainer.Children.Clear();
            currentVectorMarker = null;
            currentContourMarker = null;

            var material = MaterialHelper.CreateMaterial(Colors.DodgerBlue, 0.6);
            var model = new WpfMedia.GeometryModel3D(mesh, material) { BackMaterial = material };

            ModelContainer.Children.Add(new WpfMedia.ModelVisual3D { Content = model });

            CalculateFlux(mesh);
            Viewport.ZoomExtents();
        }

        private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (currentMesh.Positions.Count == 0 || !isFieldValid) return;

            Point mousePos = e.GetPosition(Viewport);
            bool isHit = Viewport3DHelper.FindNearest(Viewport.Viewport, mousePos, out WpfMedia.Point3D hitPoint, out WpfMedia.Vector3D normal, out DependencyObject visual);

            if (isHit && visual != currentVectorMarker && visual != currentContourMarker)
            {
                DrawFieldVector(hitPoint);
            }
        }

        private void DrawFieldVector(WpfMedia.Point3D p)
        {
            if (currentVectorMarker != null)
            {
                ModelContainer.Children.Remove(currentVectorMarker);
            }

            var fieldA = EvaluateVectorField(p.X, p.Y, p.Z);

            var mb = new HelixGeo.MeshBuilder();
            var endPoint = V3(p.X + fieldA.X * 0.3, p.Y + fieldA.Y * 0.3, p.Z + fieldA.Z * 0.3);
            mb.AddArrow(V3(p.X, p.Y, p.Z), endPoint, 0.05f);

            var vectorModel = new WpfMedia.GeometryModel3D(ConvertMesh(mb.ToMesh()), Materials.Red);
            currentVectorMarker = new WpfMedia.ModelVisual3D { Content = vectorModel };
            ModelContainer.Children.Add(currentVectorMarker);
        }

        private void CalculateFlux(WpfMedia.MeshGeometry3D mesh)
        {
            if (!UpdateVectorFieldExpressions()) return;

            double totalFlux = 0;
            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3)
            {
                var p1 = mesh.Positions[mesh.TriangleIndices[i]];
                var p2 = mesh.Positions[mesh.TriangleIndices[i + 1]];
                var p3 = mesh.Positions[mesh.TriangleIndices[i + 2]];

                double cx = (p1.X + p2.X + p3.X) / 3.0;
                double cy = (p1.Y + p2.Y + p3.Y) / 3.0;
                double cz = (p1.Z + p2.Z + p3.Z) / 3.0;

                var fieldA = EvaluateVectorField(cx, cy, cz);

                double v1x = p2.X - p1.X; double v1y = p2.Y - p1.Y; double v1z = p2.Z - p1.Z;
                double v2x = p3.X - p1.X; double v2y = p3.Y - p1.Y; double v2z = p3.Z - p1.Z;

                double dSx = (v1y * v2z - v1z * v2y) * 0.5;
                double dSy = (v1z * v2x - v1x * v2z) * 0.5;
                double dSz = (v1x * v2y - v1y * v2x) * 0.5;

                totalFlux += (fieldA.X * dSx + fieldA.Y * dSy + fieldA.Z * dSz);
            }
            FluxResult.Text = $"Ф = {totalFlux:F4}";
        }
    }
}