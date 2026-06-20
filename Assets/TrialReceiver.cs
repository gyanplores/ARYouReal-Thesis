using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

public class TrialReceiver : MonoBehaviour
{
    public int listenPort = 5005;

    public SensoryManager sensoryManager;
    public Transform droneTransform;

    public bool mirrorX = true;
    public bool mirrorY = false;
    public bool mirrorZ = false;

    public float moveSpeed = 3f;

    private UdpClient udp;
    private Thread thread;
    private bool running;

    private readonly object lockObj = new object();

    private Vector3 targetPos;
    private string affordance = "";
    private bool hasNew = false;

    void Start()
    {
        if (droneTransform == null)
            droneTransform = transform;

        udp = new UdpClient(listenPort);
        running = true;

        thread = new Thread(Loop);
        thread.IsBackground = true;
        thread.Start();

        Debug.Log("Listening on " + listenPort);
    }

    void Loop()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref ep);
                string msg = Encoding.UTF8.GetString(data);

                Parse(msg);
            }
            catch { }
        }
    }

    void Parse(string json)
    {
        if (!json.Contains("\"TRIAL\"")) return;

        string aff = Get(json, "affordance");
        float x = GetFloat(json, "x");
        float y = GetFloat(json, "y");
        float z = GetFloat(json, "z");

        Vector3 real = new Vector3(x, y, z);

        Vector3 mirrored = new Vector3(
            mirrorX ? -x : x,
            mirrorY ? -y : y,
            mirrorZ ? -z : z
        );

        lock (lockObj)
        {
            targetPos = mirrored;
            affordance = aff;
            hasNew = true;
        }
    }

    void Update()
    {
        bool newData;
        Vector3 target;
        string aff;

        lock (lockObj)
        {
            newData = hasNew;
            hasNew = false;

            target = targetPos;
            aff = affordance;
        }

        if (newData)
        {
            if (sensoryManager != null)
                sensoryManager.ApplyAffordanceString(aff);
        }

        droneTransform.position = Vector3.MoveTowards(
            droneTransform.position,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    string Get(string json, string key)
    {
        string k = $"\"{key}\"";
        int i = json.IndexOf(k);
        int c = json.IndexOf(":", i);
        int q1 = json.IndexOf("\"", c + 1);
        int q2 = json.IndexOf("\"", q1 + 1);
        return json.Substring(q1 + 1, q2 - q1 - 1);
    }

    float GetFloat(string json, string key)
    {
        string k = $"\"{key}\"";
        int i = json.IndexOf(k);
        int c = json.IndexOf(":", i);

        int end = c + 1;
        while (end < json.Length &&
               json[end] != ',' &&
               json[end] != '}')
        {
            end++;
        }

        string val = json.Substring(c + 1, end - c - 1).Trim();
        float.TryParse(val, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float r);
        return r;
    }

    void OnDestroy()
    {
        running = false;
        udp?.Close();
        thread?.Abort();
    }
}
