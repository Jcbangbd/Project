using UnityEngine;
using System.Collections;

public abstract class BufferBase : DefinedItem 
{

    //public override DefinedType UserType
    //{
    //    get { throw new System.NotImplementedException(); }
    //}

    /// <summary>
    /// 根据集装箱名称查找集装箱
    /// </summary>
    /// <param name="containerName">集装箱名称</param>
    /// <returns>Container</returns>
    public abstract Container FindContainer(string containerName);

    public abstract void DestroyAllContainer();
}
