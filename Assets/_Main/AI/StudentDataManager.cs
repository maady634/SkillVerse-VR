using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class StudentEntry
{
    public string name;
    public int assessment;
    public int training;
}

[System.Serializable]
public class StudentList
{
    public List<StudentEntry> students = new List<StudentEntry>();
}

public class StudentDataManager : MonoBehaviour
{
    private string filePath;
    private StudentList studentList = new StudentList();

    void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "students.json");

        // Load existing data if available
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            studentList = JsonUtility.FromJson<StudentList>(json);
        }
    }

    public void AddStudent(string name, int assessment, int training)
    {
        StudentEntry newEntry = new StudentEntry
        {
            name = name,
            assessment = assessment,
            training = training
        };

        studentList.students.Add(newEntry);

        string json = JsonUtility.ToJson(studentList, true);
        File.WriteAllText(filePath, json);

        Debug.Log("Student added and saved.");
    }
}
