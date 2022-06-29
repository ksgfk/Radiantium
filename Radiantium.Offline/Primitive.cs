using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public abstract class Primitive
    {
        /// <summary>
        /// 材质
        /// </summary>
        public abstract Material? Material { get; }

        /// <summary>
        /// 灯光
        /// </summary>
        public abstract AreaLight? Light { get; set; }

        /// <summary>
        /// 世界空间下的包围盒
        /// </summary>
        public abstract BoundingBox3F WorldBound { get; }

        /// <summary>
        /// 参与介质
        /// </summary>
        public abstract MediumAdapter Medium { get; }

        /// <summary>
        /// 光线求交
        /// </summary>
        /// <param name="ray">光线</param>
        /// <returns>是否存在交点</returns>
        public abstract bool Intersect(Ray3F ray);

        /// <summary>
        /// 光线求交
        /// </summary>
        /// <param name="ray">光线</param>
        /// <param name="inct">返回交点信息</param>
        /// <returns>是否存在交点</returns>
        public abstract bool Intersect(Ray3F ray, out Intersection inct);
    }

    public class GeometricPrimitive : Primitive
    {
        public Shape Shape { get; }
        public override BoundingBox3F WorldBound => Shape.WorldBound;
        public override Material? Material { get; }
        public override AreaLight? Light { get; set; }
        public override MediumAdapter Medium { get; }

        public GeometricPrimitive(Shape shape, Material? material, MediumAdapter medium)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Material = material;
            Medium = medium ?? throw new ArgumentNullException(nameof(medium));
        }

        public override bool Intersect(Ray3F ray)
        {
            return Shape.Intersect(ray);
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            bool isHit = Shape.Intersect(ray, out SurfacePoint surface);
            if (isHit)
            {
                ShapeIntersection shape = Shape.GetIntersection(ray, surface);
                inct = new Intersection(shape.P, shape.UV, shape.T, this, shape.Shading, shape.Wr);
            }
            else
            {
                inct = default;
            }
            return isHit;
        }
    }

    /// <summary>
    /// 多个图元聚合, 是空间加速结构的基类
    /// </summary>
    public abstract class Aggregate : Primitive
    {
        public sealed override Material? Material => throw new NotSupportedException("aggregate can't have material");
        public sealed override AreaLight? Light
        {
            get => throw new NotSupportedException("aggregate can't have light");
            set => throw new NotSupportedException("aggregate can't have light");
        }
        public sealed override MediumAdapter Medium => throw new NotSupportedException("aggregate can't have medium");
    }

    public class InstancedTransform
    {
        public Matrix4x4 ModelToWorld { get; }
        public Matrix4x4 WorldToModel { get; }

        public InstancedTransform(Matrix4x4 modelToWorld)
        {
            ModelToWorld = modelToWorld;
            if (!Matrix4x4.Invert(ModelToWorld, out Matrix4x4 inv))
            {
                throw new ArgumentException("invalid transform matrix");
            }
            WorldToModel = inv;
        }
    }

    public class ShapeWrapperPrimitive : Primitive
    {
        public Shape Shape { get; }
        public override Material? Material => throw new NotSupportedException("wrapper hasn't material");
        public override AreaLight? Light { get => null; set => throw new NotSupportedException("wrapper can't set light"); }
        public override BoundingBox3F WorldBound => Shape.WorldBound;
        public override MediumAdapter Medium => throw new NotSupportedException("wrapper hasn't medium");

        public ShapeWrapperPrimitive(Shape shape)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        }

        public override bool Intersect(Ray3F ray)
        {
            return Shape.Intersect(ray);
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            bool isHit = Shape.Intersect(ray, out SurfacePoint surface);
            if (isHit)
            {
                ShapeIntersection shape = Shape.GetIntersection(ray, surface);
                inct = new Intersection(shape.P, shape.UV, shape.T, this, shape.Shading, shape.Wr);
            }
            else
            {
                inct = default;
            }
            return isHit;
        }
    }

    public class InstancedPrimitive : Primitive
    {
        public Primitive Instanced { get; }
        public InstancedTransform Transform { get; }
        public override Material? Material { get; }
        public override AreaLight? Light { get => null; set => throw new NotSupportedException("InstancedPrimitive can't set light"); }
        public override BoundingBox3F WorldBound { get; }
        public override MediumAdapter Medium { get; }

        public InstancedPrimitive(Primitive instanced, InstancedTransform transform, Material? material, MediumAdapter medium)
        {
            Instanced = instanced ?? throw new ArgumentNullException(nameof(instanced));
            Transform = transform ?? throw new ArgumentNullException(nameof(transform));
            Material = material;
            Medium = medium ?? throw new ArgumentNullException(nameof(medium));
            WorldBound = BoundingBox3F.Transform(Instanced.WorldBound, Transform.ModelToWorld);
        }

        public override bool Intersect(Ray3F ray)
        {
            Matrix4x4 toModel = Transform.WorldToModel;
            Ray3F modelSpaceRay = Ray3F.Transform(ray, toModel);
            return Instanced.Intersect(modelSpaceRay);
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            Matrix4x4 toModel = Transform.WorldToModel;
            Matrix4x4 toWorld = Transform.ModelToWorld;
            Ray3F modelSpaceRay = Ray3F.Transform(ray, toModel);
            bool anyHit = Instanced.Intersect(modelSpaceRay, out Intersection modelInct);
            if (anyHit)
            {
                Vector3 p = Vector3.Transform(modelInct.P, toWorld);
                Vector3 n = Vector3.Normalize(Vector3.TransformNormal(modelInct.N, toWorld));
                Coordinate coord = new Coordinate(n);
                Vector3 wr = Vector3.Normalize(Vector3.TransformNormal(modelInct.Wr, toWorld));
                inct = new Intersection(p, modelInct.UV, Vector3.Distance(ray.O, p), this, coord, wr);
            }
            else
            {
                inct = default;
            }
            return anyHit;
        }
    }
}
