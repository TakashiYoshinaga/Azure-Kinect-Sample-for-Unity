using Microsoft.Azure.Kinect.Sensor;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PointCloudScript : MonoBehaviour
{
    //Variable for handling Kinect
    Device kinect;
    //Number of all points of PointCloud 
    int num;
    //Used to draw a set of points
    Mesh mesh;
    //Array of coordinates for each point in PointCloud
    Vector3[] vertices;
    //Array of colors corresponding to each point in PointCloud
    Color32[] colors;
    //List of indexes of points to be rendered
    int[] indices;
    //Class for coordinate transformation(e.g.Color-to-depth, depth-to-xyz, etc.)
    Transformation transformation;

    void Start()
    {
        //The method to initialize Kinect
        InitKinect();
        //Initialization for point cloud rendering
        InitMesh();
        //Loop to get data from Kinect and rendering
        Task t = KinectLoop();
    }

    //Initialization of Kinect
    private void InitKinect()
    {
        //Connect with the 0th Kinect
        kinect = Device.Open(0);
        //Setting the Kinect operation mode and starting it
        kinect.StartCameras(new DeviceConfiguration
        {
            ColorFormat = ImageFormat.ColorBGRA32,
            ColorResolution = ColorResolution.R720p,
            DepthMode = DepthMode.NFOV_Unbinned,
            SynchronizedImagesOnly = true,
            CameraFPS = FPS.FPS30
        });
        //Access to coordinate transformation information
        transformation = kinect.GetCalibration().CreateTransformation();
    }

    //Prepare to draw point cloud.
    private void InitMesh()
    {
        //Get the width and height of the Depth image and calculate the number of all points
        int width = kinect.GetCalibration().DepthCameraCalibration.ResolutionWidth;
        int height = kinect.GetCalibration().DepthCameraCalibration.ResolutionHeight;
        num = width * height;

        //Instantiate mesh
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        //Allocation of vertex and color storage space for the total number of pixels in the depth image
        vertices = new Vector3[num];
        colors = new Color32[num];
        indices = new int[num];

        //Initialization of index list
        for (int i = 0; i < num; i++)
        {
            indices[i] = i;
        }


        //Allocate a list of point coordinates, colors, and points to be drawn to mesh
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.SetIndices(indices, MeshTopology.Points, 0);

        gameObject.GetComponent<MeshFilter>().mesh = mesh;
    }

    private async Task KinectLoop()
    {
        while (true)
        {
            using (Capture capture = await Task.Run(() => kinect.GetCapture()).ConfigureAwait(true))
            {
                //Getting color information
                Image colorImage = transformation.ColorImageToDepthCamera(capture);
                BGRA[] colorArray = colorImage.GetPixels<BGRA>().ToArray();

                //Getting vertices of point cloud
                Image xyzImage = transformation.DepthImageToPointCloud(capture.Depth);
                Short3[] xyzArray = xyzImage.GetPixels<Short3>().ToArray();

                for (int i = 0; i < num; i++)
                {
                    vertices[i].x = xyzArray[i].X * 0.001f;
                    vertices[i].y = -xyzArray[i].Y * 0.001f;//上下反転
                    vertices[i].z = xyzArray[i].Z * 0.001f;

                    colors[i].b = colorArray[i].B;
                    colors[i].g = colorArray[i].G;
                    colors[i].r = colorArray[i].R;
                    colors[i].a = 255;
                }

                mesh.vertices = vertices;
                mesh.colors32 = colors;
                mesh.RecalculateBounds();
            }
        }
    }

    //Stop Kinect as soon as this object disappear
    private void OnDestroy()
    {
        kinect.StopCameras();
    }

}
