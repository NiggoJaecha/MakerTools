using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vectrosity;
using BepInEx;
using BepInEx.Logging;
using KKAPI.Maker;

namespace MakerTools
{
    class MakerTools_Cursor3D
    {
        //private ManualLogSource Logger = MakerTools.Logger;

        public Vector3 position { get => cPosition; set => positionCursor(value, cDirection); }
        public Vector3 direction { get => cDirection; set => positionCursor(cPosition, value); }

        private Vector3 cPosition;
        private Vector3 cDirection;
        private float cRadius = 0.005f;
        private int cPoints;
        private Color cColor;

        // VecorLine for CursorVisuals
        private VectorLine cLine;


        public MakerTools_Cursor3D(int circlePoints = 16)
        {
            cPoints = circlePoints;
            cColor = Color.yellow;
            MakerTools.Instance.CameraMovedEvent += CamMoved;
        }

        public MakerTools_Cursor3D(Color color, int circlePoints = 16)
        {
            cPoints = circlePoints;
            cColor = color;
            MakerTools.Instance.CameraMovedEvent += CamMoved;
        }

        public void Destroy()
        {
            MakerTools.Instance.CameraMovedEvent -= CamMoved;
            VectorLine.Destroy(ref cLine);
        }

        private void CamMoved(object sender, CameraMovedEventArgs e)
        {
            visualUpdate();
        }

        public void visualUpdate()
        {
            if (cLine != null) cLine.Draw();
        }

        private List<Vector3> CalculateCircle(Vector3 center, Vector3 normal, float radius, int circlePoints = 8)
        {
            // code provided by ChatGPT ;)
            Vector3 normalizedN = normal.normalized;
            Vector3 vectorU = Vector3.Cross(normalizedN, Vector3.right).normalized;
            Vector3 vectorV = Vector3.Cross(normalizedN, vectorU).normalized;

            float theta = 360f / circlePoints;

            List<Vector3> polygon = new List<Vector3>();

            // Calculate the eight points of the octagon
            for (int i = 0; i < circlePoints; i++)
            {
                float angle = i * theta;
                // Convert the angle to radians
                float radians = angle * Mathf.Deg2Rad;

                // Calculate the position of the point on the octagon
                Vector3 position = center + radius * (Mathf.Cos(radians) * vectorU + Mathf.Sin(radians) * vectorV);

                polygon.Add(position);
            }
            return polygon;
        }

        public void positionCursor(Vector3 position, Vector3 direction)
        {
            if (cLine == null)
            {
                List<Vector3> linePoints = CalculateCircle(position, direction, cRadius, cPoints);
                linePoints.Add(linePoints[0]);
                linePoints.Add(position);
                linePoints.Add(position + (direction.normalized * 0.015f));
                cLine = new VectorLine("MakerToolsCursor3DLine", linePoints, 3f, LineType.Continuous);
                cLine.color = cColor;
                cLine.layer = 10;
            }
            else
            {
                List<Vector3> linePoints = CalculateCircle(position, direction, cRadius, cPoints);
                linePoints.Add(linePoints[0]);
                linePoints.Add(position);
                linePoints.Add(position + (direction.normalized * 0.015f));
                cLine.points3 = linePoints;
            }
            this.cPosition = position;
            this.cDirection = direction.normalized;
            visualUpdate();
        }

        public void positionCursor(RaycastHit hit)
        {
            positionCursor(hit.point, hit.normal);
        }
    }
}
