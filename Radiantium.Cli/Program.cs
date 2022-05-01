using Radiantium.Core;
using Radiantium.Offline;
using Radiantium.Offline.Accelerators;
using Radiantium.Offline.Integrators;
using Radiantium.Offline.Shapes;
using System.Numerics;

const int w = 768;
const int h = 768;
Camera camera = new PerspectiveCamera(
    30,
    0.001f,
    100,
    new Vector3(-65.6055f, 47.5762f, 24.3583f),
    new Vector3(-64.8161f, 47.2211f, 23.8576f),
    new Vector3(0.299858f, 0.934836f, -0.190177f),
    w, h
);
TriangleModel model;
{
    using var reader = new WavefrontObjReader(File.OpenRead(@"D:\ProjectC++\nori\scenes\pa2\ajax.obj"));
    reader.Read();
    if (!string.IsNullOrWhiteSpace(reader.ErrorInfo)) { Console.WriteLine(reader.ErrorInfo); }
    model = reader.AllFacesToModel();
}
TriangleMesh mesh = new TriangleMesh(Matrix4x4.Identity, model);
var triList = mesh.ToTriangle();
Aggregate agg = new Octree(triList.Select(t => new GeometricPrimitive(t)).Cast<Primitive>().ToList(), outBound: 2f, maxDepth: 12, maxCount: 4);
Scene s = new Scene(agg);
Renderer r = new Renderer(s, camera, new AmbientOcclusion(), 256);
Task t = r.Start();
t.Wait();
Console.WriteLine(r.RenderUseTime.TotalMilliseconds);
using var stream = File.OpenWrite(@"C:\Users\ksgfk\Desktop\test.exr");
r.RenderTarget.SaveOpenExr(stream);
