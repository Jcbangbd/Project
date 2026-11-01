using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ZPMC.VR.Unity
{
    public class SequenceList<T> where T : IComparable<T>
    {
        /// <summary>
        /// 链表头节点
        /// </summary>
        Node<T> head { get; set; }
        /// <summary>
        /// 链表尾节点
        /// </summary>
        public Node<T> tail { get; set; }

        private int count;
        /// <summary>
        /// 链表大小
        /// </summary>
        public int Count
        {
            get { return this.count; }
        }

        /// <summary>
        /// 判断链表是否是空的
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return head == null;
            }
        }

        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="t">要插入的数据</param>
        public void Enqueue(T t)
        {
            Node<T> node = new Node<T>(t);

            //如果链表是空的，则插在链表头部
            if (IsEmpty )
            {
                this.head = node;
                this.tail = node;
                count = 1;// count++;
                return ;
            }
            Node<T> current = this.head; //当前节点
            //如果索引为0，意味着向链表头部添加节点。
            if (t.CompareTo(head.Instance) <0)
            {
                this.head = node;
                node.Next = current;
                count++;
                return ;
            }

            //要插入位置的节点
            while (current.Next != null)
            {
                if (t.CompareTo(current.Next.Instance)<0)
                {
                    node.Next = current.Next;
                    current.Next = node;
                    count++;
                    return ;
                }
                current = current.Next;
            }

            // 插在最后了
            current.Next = node;
            this.tail = node;
            count++;
            return ;
        }

        /// <summary>
        /// 插入数组，不考虑排序
        /// </summary>
        /// <param name="t">数组</param>
        public void EnqueueArrayAtTail(T[] t)
        {
            if (t.Length == 0)
                return;
            //Node<T> last = this.head;
            Node<T> node = new Node<T>(t[0]);
            //先插入第一个结点
            if (IsEmpty) // last is NULL 如果链表是空的，则插在链表头部
            {
                this.head = node;
                this.tail = node;
                //last = node;
                count++;
            }
            else
            {
                //while (last.Next != null)
                //{
                //    last = last.Next;
                //}
                //last.Next = node;
                //last = node;
                this.tail.Next = node;
                this.tail = node;
                count++;
            }
            //再插入后面n-1个结点
            for (int i = 1; i < t.Length; i++)
            {
                node = new Node<T>(t[i]);
                //last.Next = node;
                //last = node;
                this.tail.Next = node;
                this.tail = node;
                count++;
            }
        }

        /// <summary>
        /// 移除头节点
        /// </summary>
        public T Dequeue()
        {
            Node<T> first = head;
            ////链表头节点是空的
            //if (IsEmpty)
            //{
            //    throw new Exception("链表是空的。");
            //}
            //如果size为1，那就是清空链表。
            if (count == 1)
            {
                Clear();
            }
            else
            {                
                head = head.Next;//将头节点设为原头结点的下一个节点，就是下一个节点上移
            }
            count--;
            return first.Instance;
        }

        /// <summary>
        /// 返回头部节点数据，在列表长度大于0时调用
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            return head.Instance;
        }

        public T[] ConvertToArray()
        {
            T[] array = new T[this.count];

            int i = 0;
            Node<T> current = head;
            while (current != null)
            {
                array[i] = current.Instance;
                current = current.Next;
                i++;
            }
            return array;
        }

        public T[] ConvertToArrayInverse()
        {
            T[] array = new T[this.count];

            int i = this.count - 1;
            Node<T> current = head;
            while (current != null)
            {
                array[i] = current.Instance;
                current = current.Next;
                i--;
            }
            return array;
        }

        /// <summary>
        /// 清除链表中的数据
        /// </summary>
        public void Clear()
        {
            head = null;
            tail = null;
            count = 0;
        }
    }

    public class Node<T> where T : IComparable<T>
    {
        public T Instance;
        public Node<T> Next { get; set; }

        public Node()
        {
            this.Instance = default(T);
            this.Next = null;
        }

        public Node(T data)
        {
            this.Instance = data;
        }
    } 
}