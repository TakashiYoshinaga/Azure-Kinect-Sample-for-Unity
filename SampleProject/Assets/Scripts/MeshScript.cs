using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.Azure.Kinect.Sensor;
using System.Threading.Tasks;

public class MeshScript : MonoBehaviour
{
    //Variable for handling Kinect
    Device kinect;
    //Width and Height of Depth image.
    int depthWidth;
    int depthHeight;
    //Number of all points of PointCloud 
    int num;
    //Used to draw a set of points
    Mesh mesh;
    //Array of coordinates for each point in PointCloud
    Vector3[] vertices;
    //Array of colors corresponding to each point in PointCloud
    Color32[] colors;
    //List of indexes of points to be rendered
    int[] indeces;
    //Color image to be attatched to mesh
    Texture2D texture;
    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    Transformation transformation;

    int nearClip = 300;

    //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        kinect.StopCameras();
    }

    void Start()
    {
        //The method to initialize Kinect
        InitKinect();
        //Initialization for colored mesh rendering
        InitMesh();
        //Loop to get data from Kinect and rendering
        Task t = KinectLoop(kinect);
    }

    //Initialization of Kinect
    void InitKinect()
    {
        //Connect with the 0th Kinect
        kinect = Device.Open(0);
        //Setting the Kinect operation mode and starting it
        kinect.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NFOV_2x2Binned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30,
        });
        //Access to coordinate transformation information
        transformation = kinect.GetCalibration().CreateTransformation();



    }

    //Prepare to draw colored mesh
    void InitMesh()
    {
        //Get the width and height of the Depth image and calculate the number of all points
        depthWidth = kinect.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        depthHeight = kinect.GetCalibration().DepthCameraCalibration.ResolutionHeight;
        num = depthWidth * depthHeight;

        //Instantiate mesh
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        //Allocation of vertex and color storage space for the total number of pixels in the depth image
        vertices = new Vector3[num];
        colors = new Color32[num];
        texture = new Texture2D(depthWidth, depthHeight);
        Vector2[] uv = new Vector2[num];
        Vector3[] normals = new Vector3[num];
        indeces = new int[6 * (depthWidth - 1) * (depthHeight - 1)];

        //Initialization of uv and normal 
        int index = 0;
        for (int y = 0; y < depthHeight; y++)
        {
            for (int x = 0; x < depthWidth; x++)
            {
                uv[index] = new Vector2(((float)(x + 0.5f) / (float)(depthWidth)), ((float)(y + 0.5f) / ((float)(depthHeight))));
                normals[index] = new Vector3(0, -1, 0);
                index++;
            }
        }

        //Allocate a list of point coordinates, colors, and points to be drawn to mesh
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
                //Getting color information
                Image modifiedColor = transformation.ColorImageToDepthCamera(capture);
                BGRA[] colorArray = modifiedColor.GetPixels<BGRA>().ToArray();

                //Getting vertices of point cloud
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

                        colors[pointIndex].a = 255;
                        colors[pointIndex].b = colorArray[pointIndex].B;
                        colors[pointIndex].g = colorArray[pointIndex].G;
                        colors[pointIndex].r = colorArray[pointIndex].R;

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

                texture.SetPixels32(colors);
                texture.Apply();

                mesh.vertices = vertices;

                mesh.triangles = indeces;
                mesh.RecalculateBounds();
            }
        }
    }

}
