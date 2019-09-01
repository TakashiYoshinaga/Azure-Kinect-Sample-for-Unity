using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;

public class KinectScript : MonoBehaviour
{
    int depthWidth;
    int depthHeight;
    int pointNum;
    int nearClip = 300;

    Mesh mesh;
    Vector3[] vertices;
    int[] indeces;
    Color32[] col;
    Texture2D texture;

    Transformation transformation;
    Device device;

    private void OnDestroy()
    {
        device.StopCameras();
    }

    void Start()
    {
        InitKinect(); 
        InitMesh();
        Task t = KinectLoop(device);
    }
    void InitKinect()
    {
        device = Device.Open(0);
        device.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NFOV_Unbinned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        });

        transformation = device.GetCalibration().CreateTransformation();

        depthWidth = device.GetCalibration().depth_camera_calibration.resolution_width;
        depthHeight = device.GetCalibration().depth_camera_calibration.resolution_height;
        pointNum = depthWidth * depthHeight;

    }
    void InitMesh()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        
        texture = new Texture2D(depthWidth, depthHeight);
        col = new Color32[pointNum];
       
        vertices = new Vector3[pointNum];
        Vector2[] uv = new Vector2[pointNum];

        Vector3[] normals = new Vector3[pointNum];
        indeces = new int[6 * (depthWidth - 1) * (depthHeight - 1)];
        int index = 0;
        for (int y = 0; y < depthHeight; y++)
        {
            for (int x = 0; x < depthWidth; x++)
            {
                uv[index] = new Vector2(((float)(x+0.5f) / (float)(depthWidth)), ((float)(y+0.5f) / ((float)(depthHeight))));
                normals[index] = new Vector3(0, -1, 0);
                index++;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.normals = normals;
        gameObject.GetComponent<MeshRenderer>().materials[0].mainTexture = texture;

        gameObject.GetComponent<MeshFilter>().mesh = mesh;
    }

    private async Task KinectLoop(Device device)
    {

        while (true)
        {

            using (Capture capture = await Task.Run(() => device.GetCapture()).ConfigureAwait(true))
            {
                Image modifiedColor = transformation.ColorImageToDepthCamera(capture);
                BGRA[] colorArray = modifiedColor.GetPixels<BGRA>().ToArray();
                Image cloudImage = transformation.DepthImageToPointCloud(capture.Depth);
                Short3[] PointCloud = cloudImage.GetPixels<Short3>().ToArray();

                int triangleIndex = 0;
                int pointIndex = 0;
                int topLeft, topRight, bottomLeft, bottomRight;
                int tl, tr, bl, br;
                for (int y = 0; y < depthHeight; y++)
                {
                    for (int x = 0; x < depthWidth; x++)
                    {
                       
                        vertices[pointIndex].x = PointCloud[pointIndex].X * 0.001f;
                        vertices[pointIndex].y = -PointCloud[pointIndex].Y * 0.001f;
                        vertices[pointIndex].z = PointCloud[pointIndex].Z * 0.001f;

                        col[pointIndex].a = 255;
                        col[pointIndex].b = colorArray[pointIndex].B;
                        col[pointIndex].g = colorArray[pointIndex].G;
                        col[pointIndex].r = colorArray[pointIndex].R;

                        if (x != (depthWidth - 1) && y != (depthHeight - 1))
                        {
                            topLeft = pointIndex;
                            topRight = topLeft + 1;
                            bottomLeft = topLeft + depthWidth;
                            bottomRight = bottomLeft + 1;
                            tl = PointCloud[topLeft].Z;
                            tr = PointCloud[topRight].Z;
                            bl = PointCloud[bottomLeft].Z;
                            br = PointCloud[bottomRight].Z;

                            if (tl > nearClip && tr > nearClip && bl > nearClip)
                            {
                                indeces[triangleIndex++] = topLeft;
                                indeces[triangleIndex++] = topRight;
                                indeces[triangleIndex++] = bottomLeft;
                            }
                            else
                            {
                                indeces[triangleIndex++] = 0;
                                indeces[triangleIndex++] = 0;
                                indeces[triangleIndex++] = 0;
                            }

                            if (bl > nearClip && tr > nearClip && br > nearClip)
                            {
                                indeces[triangleIndex++] = bottomLeft;
                                indeces[triangleIndex++] = topRight;
                                indeces[triangleIndex++] = bottomRight;
                            }
                            else
                            {
                                indeces[triangleIndex++] = 0;
                                indeces[triangleIndex++] = 0;
                                indeces[triangleIndex++] = 0;
                            }
                        }
                        pointIndex++;
                    }
                }

                texture.SetPixels32(col);
                texture.Apply();
               
                mesh.vertices = vertices;

                mesh.triangles = indeces;
                mesh.RecalculateBounds();
            }
        }
    }
}
