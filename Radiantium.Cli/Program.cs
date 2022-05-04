using Radiantium.Core;
using Radiantium.Offline;
using Radiantium.Offline.Accelerators;
using Radiantium.Offline.Integrators;
using Radiantium.Offline.Lights;
using Radiantium.Offline.Materials;
using Radiantium.Offline.Shapes;
using System.Numerics;

const int w = 800;
const int h = 600;
Camera camera = new PerspectiveCamera(
    27.7856f,
    0.001f,
    100,
    new Vector3(0f,
        0.919769f,
        5.41159f),
    new Vector3(0,
        0.893051f,
        4.41198f),
    new Vector3(0, 1, 0),
    w, h
);

var left = GetTriangle(@"D:\ProjectC#\Pursuit\Scene\cbox\meshes\leftwall.obj");
var right = GetTriangle(@"D:\ProjectC#\Pursuit\Scene\cbox\meshes\rightwall.obj");
var other = GetTriangle(@"D:\ProjectC#\Pursuit\Scene\cbox\meshes\walls.obj");
var light = GetTriangle(@"D:\ProjectC#\Pursuit\Scene\cbox\meshes\light.obj");

var leftP = left.Select(t => new GeometricPrimitive(t, new DiffuseReflection(new Color3F(0.630f, 0.065f, 0.05f))));
var rightP = right.Select(t => new GeometricPrimitive(t, new DiffuseReflection(new Color3F(0.161f, 0.133f, 0.427f))));
var otherP = other.Select(t => new GeometricPrimitive(t, new DiffuseReflection(new Color3F(0.725f, 0.71f, 0.68f))));
var lightP = light.Select(t => new GeometricPrimitive(t, new DiffuseReflection(new Color3F(0.5f)))).ToArray();

var ls = lightP.Select(t =>
{
    var l = new DiffuseAreaLight(t.Shape, new Color3F(40));
    t.Light = l;
    return l;
}).ToArray();

var resL = leftP.Concat(rightP).Concat(otherP).Concat(lightP).Cast<Primitive>().ToList();
resL.Add(new GeometricPrimitive(new Sphere(0.35f, Matrix4x4.CreateTranslation(0.5f, 0.35f, 0.2f)), new PerfectGlass(new Color3F(1),new Color3F(1), 1, 1.5f)));
resL.Add(new GeometricPrimitive(new Sphere(0.35f, Matrix4x4.CreateTranslation(-0.5f, 0.35f, -0.25f)), new PrefectMirror(new Color3F(1))));
//resL.Add(new GeometricPrimitive(new Sphere(0.35f, Matrix4x4.CreateTranslation(0.5f, 0.35f, 0.2f)), new DiffuseReflection(new Color3F(0.5f))));
//resL.Add(new GeometricPrimitive(new Sphere(0.35f, Matrix4x4.CreateTranslation(-0.5f, 0.35f, -0.25f)), new DiffuseReflection(new Color3F(0.5f))));

//Aggregate agg = new Octree(resL, outBound: 2, maxDepth: 10, maxCount: 10);
Aggregate agg = new Bvh(resL, 1, SplitMethod.SAH);
Scene s = new Scene(agg, ls, Array.Empty<Light>());
Renderer r = new Renderer(s, camera, new PathTracing(9, 1.0f, PathTracingMethod.Nee), 256);
//Renderer r = new Renderer(s, camera, new AmbientOcclusion(), 32);
Task t = r.Start();
t.Wait();
Console.WriteLine(r.RenderUseTime.TotalMilliseconds);
using var stream = File.OpenWrite(@"C:\Users\ksgfk\Desktop\test.exr");
r.RenderTarget.SaveOpenExr(stream);

static TriangleModel GetModel(string path)
{
    using var reader = new WavefrontObjReader(File.OpenRead(path));
    reader.Read();
    if (!string.IsNullOrWhiteSpace(reader.ErrorInfo)) { Console.WriteLine(reader.ErrorInfo); }
    return reader.AllFacesToModel();
}

static List<Triangle> GetTriangle(string path)
{
    TriangleModel model1 = GetModel(path);
    TriangleMesh mesh1 = new TriangleMesh(Matrix4x4.Identity, model1);
    return mesh1.ToTriangle();
}