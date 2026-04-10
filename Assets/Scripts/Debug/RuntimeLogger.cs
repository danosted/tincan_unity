// using UnityEngine;
// using System.Collections.Generic;
// using System.IO;

// [System.Serializable]
// public class AirshipLogEntry
// {
//     public float timestamp;
//     public Vector3 position;
//     public Quaternion rotation;
//     public float currentMoveSpeed;
//     public bool isPiloted;
//     public Vector3 rigidbodyVelocity;
//     public Vector3 rigidbodyAngularVelocity;
//     public bool rigidbodyIsKinematic;
// }

// [System.Serializable]
// public class AirshipMetadata
// {
//     public float moveSpeed;
//     public float rotationSpeed;
//     public float accelerationSmoothTime;
//     public float decelerationSmoothTime;
//     public float turnSlowdownFactor;
//     public float waypointArrivalDistance;
// }

// [System.Serializable]
// public class RuntimeLog
// {
//     public AirshipMetadata metadata;
//     public List<AirshipLogEntry> airshipEntries = new List<AirshipLogEntry>();
// }

// public class RuntimeLogger : MonoBehaviour
// {
//     private RuntimeLog _currentLog = new RuntimeLog();
//     private AirshipController _airshipController;
//     private Rigidbody _airshipRigidbody;
//     private float _startTime;

//     public static RuntimeLogger Instance { get; private set; }

//     private void Awake()
//     {
//         if (Instance != null && Instance != this)
//         {
//             Destroy(gameObject);
//         }
//         else
//         {
//             Instance = this;
//             DontDestroyOnLoad(gameObject);
//         }
//     }

//     private void Start()
//     {
//         _startTime = Time.time;
//         _airshipController = FindObjectOfType<AirshipController>();
//         if (_airshipController != null)
//         {
//             _airshipRigidbody = _airshipController.GetComponent<Rigidbody>();
//             _currentLog.metadata = new AirshipMetadata
//             {
//                 moveSpeed = _airshipController.MoveSpeed,
//                 rotationSpeed = _airshipController.RotationSpeed,
//                 accelerationSmoothTime = _airshipController.AccelerationSmoothTime,
//                 decelerationSmoothTime = _airshipController.DecelerationSmoothTime,
//                 turnSlowdownFactor = _airshipController.TurnSlowdownFactor,
//                 waypointArrivalDistance = _airshipController.WaypointArrivalDistance
//             };
//         }
//         else
//         {
//             Debug.LogWarning("RuntimeLogger: No AirshipController found in scene.");
//         }
//     }

//     private void FixedUpdate()
//     {
//         if (_airshipController != null && _airshipRigidbody != null)
//         {
//             _currentLog.airshipEntries.Add(new AirshipLogEntry
//             {
//                 timestamp = Time.time - _startTime,
//                 position = _airshipController.transform.position,
//                 rotation = _airshipController.transform.rotation,
//                 currentMoveSpeed = _airshipController.CurrentMoveSpeed,
//                 isPiloted = _airshipController.IsPiloted,
//                 rigidbodyVelocity = _airshipRigidbody.linearVelocity,
//                 rigidbodyAngularVelocity = _airshipRigidbody.angularVelocity,
//                 rigidbodyIsKinematic = _airshipRigidbody.isKinematic
//             });
//         }
//     }

//     private void OnApplicationQuit()
//     {
//         SaveLog();
//     }

//     private void SaveLog()
//     {
//         string json = JsonUtility.ToJson(_currentLog, true);
//         string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
//         string fileName = $"airship_runtime_log_{timestamp}.json";
//         string logFolderPath = Path.Combine(Application.dataPath, "../Logs");
//         if (!Directory.Exists(logFolderPath))
//         {
//             Directory.CreateDirectory(logFolderPath);
//         }
//         string filePath = Path.Combine(logFolderPath, fileName);
//         File.WriteAllText(filePath, json);
//         Debug.Log($"Airship runtime log saved to: {filePath}");
//     }
// }
