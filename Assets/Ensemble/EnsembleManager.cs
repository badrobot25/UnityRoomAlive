﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ensemble {
    public class EnsembleManager : MonoBehaviour {

        public string ensembleFolder;
        public EnsembleData Data { get; private set; }

        private List<Vector3> CameraCornerHitPositions { get; set; }

        // Use this for initialization
        void Start() {
            string ensembleFilePath = Application.dataPath +
                "/Calibrations/" + ensembleFolder + "/calibration.xml";
            Data = new EnsembleData(ensembleFilePath);

            // Set this to choose which projector this Unity instance uses.
            int localProjectorNumber = 0;
            SetCameraFromProjector(Data.projectors.ElementAt(localProjectorNumber));
        }

        void OnDrawGizmos() {
            DebugViewCameraCorners();
            if (CameraCornerHitPositions != null) {
                for (int i = 0; i < CameraCornerHitPositions.Count(); i++) {
                    Gizmos.color = Color.red;
                    Vector3 from = CameraCornerHitPositions[i];
                    Vector3 to = (i + 1 != CameraCornerHitPositions.Count()) ?
                                 CameraCornerHitPositions[i + 1] :
                                 CameraCornerHitPositions[0];
                    Gizmos.DrawLine(from, to);
                }
            }
        }

        void SetCameraFromProjector(ProjectorData projector) {
            // Take the inverse because we need to map projector's center to origin.
            Matrix4x4 worldToCameraMatrix = projector.pose.inverse;
            // Reflect in z axis because worldToCameraMatrix is OpenGL convention.
            // It uses negative z axis as the direction the camera looks in.
            worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1, 1, -1)) * worldToCameraMatrix;
            // Flip x translation, and y, z rotations to account for left-handedness
            worldToCameraMatrix[0, 3] = -worldToCameraMatrix[0, 3];
            worldToCameraMatrix[0, 1] = -worldToCameraMatrix[0, 1];
            worldToCameraMatrix[1, 0] = -worldToCameraMatrix[1, 0];
            worldToCameraMatrix[0, 2] = -worldToCameraMatrix[0, 2];
            worldToCameraMatrix[2, 0] = -worldToCameraMatrix[2, 0];
            Camera.main.worldToCameraMatrix = worldToCameraMatrix;

            Matrix4x4 projectionMatrix = ProjectionMatrixFromCameraMatrix(
                projector.cameraMatrix, projector.width, projector.height);
            Camera.main.projectionMatrix = projectionMatrix;
        }

        private void DebugViewCameraCorners() {
            List<Ray> rays = new List<Ray> {
                Camera.main.ViewportPointToRay(new Vector3(0, 0, 0)),
                Camera.main.ViewportPointToRay(new Vector3(1, 0, 0)),
                Camera.main.ViewportPointToRay(new Vector3(1, 1, 0)),
                Camera.main.ViewportPointToRay(new Vector3(0, 1, 0))
            };

            CameraCornerHitPositions = new List<Vector3>();
            foreach (Ray ray in rays) {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 1000)) {
                    CameraCornerHitPositions.Add(hit.point);
                }
            }
        }

        public static Matrix4x4 ProjectionMatrixFromCameraMatrix(Matrix4x4 cameraMatrix,
                float projectorWidth, float projectorHeight) {
            float fx = cameraMatrix[0, 0];
            float fy = cameraMatrix[1, 1];
            float cx = cameraMatrix[0, 2];
            float cy = cameraMatrix[1, 2];

            float near = 0.1f;
            float far = 100.0f;

            float w = projectorWidth;
            float h = projectorHeight;

            // fx, fy, cx, cy are in pixels
            // input coordinate system is x left, y up, z forward (right handed)
            // project to view volume where x, y in [-1, 1], z in [0, 1], x right, y up, z forward
            // pre-multiply matrix

            // -(2 * fx / w),           0,   -(2 * cx / w - 1),                           0,
            //             0,  2 * fy / h,      2 * cy / h - 1,                           0,
            //             0,           0,  far / (far - near),  -near * far / (far - near),
            //             0,           0,                   -1,                           0
            Matrix4x4 projectionRightHanded = new Matrix4x4() {
                m00 = 2 * fx / w, m01 = 0, m02 = 1 - 2 * cx / w, m03 = 0,
                m10 = 0, m11 = 2 * fy / h, m12 = 1 - 2 * cy / h, m13 = 0,
                m20 = 0, m21 = 0,          m22 = -(far + near) / (far - near), m23 = -2 * far * near / (far - near),
                m30 = 0, m31 = 0,          m32 = -1, m33 = 0
            };
            return Matrix4x4.Scale(new Vector3(-1, 1, 1)) * projectionRightHanded *
                Matrix4x4.Scale(new Vector3(-1, 1, 1));
        }

        public static Quaternion QuaternionFromMatrix(Matrix4x4 m) {
            // Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
            Quaternion q = new Quaternion();
            q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
            q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
            q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
            q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
            q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
            q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
            q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
            return q;
        }
    }
}
