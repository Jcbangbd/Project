using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class TaskInfo : IComparable<TaskInfo>
{
    public float StartTime { get; set; }
    public float EndTime { get; set; }
    public string FromCode { get; set; }
    public string ToCode { get; set; }
    public TaskItem BindTask { get; set; }
    public Container BindContainer { get; set; }
    private int currentState = 1;
    public int CurrentState
    {
        get { return currentState; }
        set { currentState = value; }
    }
    
    public int CompareTo(TaskInfo other)
    {
        return this.StartTime.CompareTo(other.StartTime);
    }
}

