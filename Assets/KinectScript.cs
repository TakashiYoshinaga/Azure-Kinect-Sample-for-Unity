using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor.WinForms;
using Microsoft.Azure.Kinect.Sensor;
using System.Runtime.InteropServices;
using System;
using System.Threading.Tasks;

public class KinectScript : MonoBehaviour
{
    int depthWidth;
    int depthHeight;
    Transformation transformation;
    Mesh mesh;
    int visNum;
    Vector3[] vertices;
    int[] vertexIndeces;
    
    Color32[] col;
    Texture2D tex;
    Device device;
    int nearThreshold = 300;

    private void OnDestroy()
    {
        device.StopCameras();
    }

    void Start()
    {

        InitKinect();

      
        InitRenderingInfo();


        Task t = KinectLoop(device);
    }
    void InitKinect()
    {
        device = Device.Open(0);
        device.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R1080p,
            DepthMode = DepthMode.NFOV_Unbinned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        });

        transformation = device.GetCalibration().CreateTransformation();

        depthWidth = device.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = device.GetCalibration().DepthCameraCalibration.ResolutionHeight;

        
    }
    void InitRenderingInfo()
    {
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        visNum = depthWidth * depthHeight;
        tex = new Texture2D(depthWidth, depthHeight);
        col = new Color32[visNum];
       
        vertices = new Vector3[visNum];
        Vector2[] uv = new Vector2[visNum];

        Vector3[] normals = new Vector3[visNum];
        vertexIndeces = new int[6 * (depthWidth - 1) * (depthHeight - 1)];
        for (int y = 0; y < depthHeight; y++)
        {
            for (int x = 0; x < depthWidth; x++)
            {
                int index = (y * depthWidth) + x;
                uv[index] = new Vector2(((float)(x+0.5f) / (float)(depthWidth)), ((float)(y+0.5f) / ((float)(depthHeight))));
                normals[index] = new Vector3(0, -1, 0);
                index++;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.normals = normals;
        gameObject.GetComponent<MeshRenderer>().materials[0].mainTexture = tex;

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
                Image hoge = transformation.DepthImageToPointCloud(capture.Depth);

                Short3[] PointCloud = hoge.GetPixels<Short3>().ToArray();


                int width = depthWidth;
                int height = depthHeight;

                int triangleIndex = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width) + x;

                        vertices[index].x = PointCloud[index].X * 0.001f;
                        vertices[index].y = -PointCloud[index].Y * 0.001f;
                        vertices[index].z = PointCloud[index].Z * 0.001f;

                        col[index].a = 255;
                        col[index].b = colorArray[index].B;
                        col[index].g = colorArray[index].G;
                        col[index].r = colorArray[index].R;

                        if (x != (width - 1) && y != (height - 1))
                        {
                            int topLeft = index;
                            int topRight = topLeft + 1;
                            int bottomLeft = topLeft + width;
                            int bottomRight = bottomLeft + 1;
                            int tl = PointCloud[index].Z;
                            int tr = PointCloud[index + 1].Z;
                            int bl = PointCloud[index + depthWidth].Z;
                            int br = PointCloud[index + 1 + depthWidth].Z;
                            if (tl > nearThreshold && tr > nearThreshold && bl > nearThreshold)
                            {
                                vertexIndeces[triangleIndex++] = topLeft;
                                vertexIndeces[triangleIndex++] = topRight;
                                vertexIndeces[triangleIndex++] = bottomLeft;
                            }
                            else
                            {
                                vertexIndeces[triangleIndex++] = 0;
                                vertexIndeces[triangleIndex++] = 0;
                                vertexIndeces[triangleIndex++] = 0;
                            }

                            if (bl > nearThreshold && tr > nearThreshold && br > nearThreshold)
                            {
                                vertexIndeces[triangleIndex++] = bottomLeft;
                                vertexIndeces[triangleIndex++] = topRight;
                                vertexIndeces[triangleIndex++] = bottomRight;
                            }
                            else
                            {
                                vertexIndeces[triangleIndex++] = 0;
                                vertexIndeces[triangleIndex++] = 0;
                                vertexIndeces[triangleIndex++] = 0;
                            }


                        }


                    }
                }

                tex.SetPixels32(col);
                tex.Apply();
               
                mesh.vertices = vertices;

                mesh.triangles = vertexIndeces;
                mesh.RecalculateBounds();
            }
        }
    }
}
