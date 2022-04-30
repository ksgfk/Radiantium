// See https://aka.ms/new-console-template for more information
using Radiantium.Core;
using Radiantium.Offline;
using System.Numerics;

Console.WriteLine("Hello, World!");

const int w = 768;
const int h = 768;

Camera camera = new PerspectiveCamera(
    45,
    0.001f,
    100,
    new Vector3(0, 2, 2),
    new Vector3(0, 0, 0),
    new Vector3(0, 1, 0),
    w, h
);
Primitive b = new Box(new BoundingBox3F(new Vector3(-0.5f), new Vector3(0.5f)));
Scene s = new Scene(b);
Renderer r = new Renderer(s, camera, new Simple(), 1);
await r.Start();
using var stream = File.OpenWrite(@"C:\Users\ksgfk\Desktop\test.png");
r.RenderTarget.SavePng(stream);

class Box : Primitive
{
    public override BoundingBox3F WorldBound { get; }

    public Box(BoundingBox3F worldBound)
    {
        WorldBound = worldBound;
    }

    public override bool Intersect(Ray3F ray)
    {
        return WorldBound.Intersect(ray);
    }
}

class Simple : Integrator
{
    public override Color3F Li(Ray3F ray, Scene scene, Random rand)
    {
        if (scene.Intersect(ray))
        {
            return new Color3F(1.0f);
        }
        return new Color3F(0.0f);
    }
}
