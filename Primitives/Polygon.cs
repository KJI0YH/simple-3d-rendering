﻿using simple_3d_rendering.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media.Media3D;

namespace Lab1.Primitives
{
    public class Polygon
    {
        public List<Vector4> Vertices { get; } = new List<Vector4>();
        public List<int> Indices { get; } = new List<int>();
        public List<Vector4> VerticesTextures { get; } = new List<Vector4>();
        public List<Vector4> VerticesNormals { get; } = new List<Vector4>();
        public Vector3 Normal { get; private set; }

        public Polygon(List<(Vector4, int)> vertices)
        {
            if (vertices.Count < 3) throw new InvalidPolygonException("Vertices count less than 3");

            foreach (var vertex in vertices)
            {
                Vertices.Add(vertex.Item1);
                Indices.Add(vertex.Item2);
            }

            Vector3 a = new Vector3(Vertices[0].X, Vertices[0].Y, Vertices[0].Z);
            Vector3 b = new Vector3(Vertices[1].X, Vertices[1].Y, Vertices[1].Z);
            Vector3 c = new Vector3(Vertices[2].X, Vertices[2].Y, Vertices[2].Z);
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Normal = Vector3.Cross(ab, ac);
        }

        public void AddVertex(List<Vector4> readVertices, int vertexIndex)
        {
            Vertices.Add(readVertices[vertexIndex]);
            Indices.Add(vertexIndex);
        }

        public void AddVertex(Vector4 vertex, int vertexIndex)
        {
            Vertices.Add(vertex);
            Indices.Add(vertexIndex);
        }

        public void AddVertexTexture(Vector4 vertexTexture)
        {
            VerticesTextures.Add(vertexTexture);
        }

        public void AddVertexNormal(Vector4 vertexNormal)
        {
            VerticesNormals.Add(vertexNormal);
        }

        public List<Polygon> Triangulate()
        {
            if (Vertices.Count == 3)
            {
                return new List<Polygon>() { this };
            }

            List<Polygon> triangles = new List<Polygon>();

            while (Vertices.Count >= 3)
            {
                List<double> relativeEarArea = new List<double>();
                for (int i = 0; i < Vertices.Count; i++)
                {
                    int indA = i;
                    int indB = (i + 1) % Vertices.Count;
                    int indC = (i == 0) ? (Vertices.Count - 1) : (i - 1);
                    Point3D pA = new Point3D(Vertices[indA].X, Vertices[indA].Y, Vertices[indA].Z);
                    Point3D pB = new Point3D(Vertices[indB].X, Vertices[indB].Y, Vertices[indB].Z);
                    Point3D pC = new Point3D(Vertices[indC].X, Vertices[indC].Y, Vertices[indC].Z);
                    Vector3D A = pB - pA;
                    Vector3D B = pC - pB;
                    Vector3D C = pA - pC;
                    double triangleSquare = (Vector3D.CrossProduct(A, B).Length) / 2;
                    double circleR = A.Length * B.Length * C.Length / 4 / triangleSquare;
                    double circleSquare = Math.PI * circleR * circleR;
                    relativeEarArea.Add(triangleSquare / circleSquare);
                }

                // Find index of ear with max relative ear area
                double maxRelativeEarArea = relativeEarArea.Max();
                int indEar = relativeEarArea.FindIndex(r => r == maxRelativeEarArea);
                int indEarRight = (indEar + 1) % Vertices.Count;
                int indEarLeft = indEar == 0 ? (Vertices.Count - 1) : (indEar - 1);

                // Clip polygon
                Polygon polygon = new Polygon(new() {
                    (Vertices[indEar], Indices[indEar]),
                    (Vertices[indEarRight], Indices[indEarRight]),
                    (Vertices[indEarLeft], Indices[indEarLeft]),
                });
                triangles.Add(polygon);

                // Delete vertex from main polygon
                Vertices.RemoveAt(indEar);
                Indices.RemoveAt(indEar);
            }

            return triangles;
        }
    }
}
