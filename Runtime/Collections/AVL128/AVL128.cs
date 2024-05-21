using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Scellecs.Morpeh.Graphics.Utilities.UnsafeHelpers;

namespace Scellecs.Morpeh.Graphics.Collections
{
    public unsafe struct AVL128<T> : IDisposable where T : unmanaged
    {
        internal struct Child
        {
            public int left;
            public int right;
        }

        internal T* values;
        internal Hash128* keys;
        internal Child* children;
        internal int* parents;
        internal sbyte* balanceFactors;

        internal int count;
        internal int capacity;
        internal int root;

        internal const int NULL = -1;

        private Allocator allocator;

        public AVL128(int capacity, Allocator allocator)
        {
            this.allocator = allocator;
            capacity = capacity <= 0 ? 4 : capacity;
            this.capacity = capacity;
            count = 0;
            root = NULL;

            values = Malloc<T>(capacity, allocator);
            keys = Malloc<Hash128>(capacity, allocator);
            children = Malloc<Child>(capacity, allocator);
            parents = Malloc<int>(capacity, allocator);
            balanceFactors = Malloc<sbyte>(capacity, allocator);
        }

        public unsafe void Resize(int newCapacity)
        {
            if (newCapacity <= capacity)
            {
                return;
            }

            T* newValues = Malloc<T>(newCapacity, allocator);
            Hash128* newKeys = Malloc<Hash128>(newCapacity, allocator);
            Child* newChildren = Malloc<Child>(newCapacity, allocator);
            int* newParents = Malloc<int>(newCapacity, allocator);
            sbyte* newBalanceFactors = Malloc<sbyte>(newCapacity, allocator);

            UnsafeUtility.MemCpy(newValues, values, UnsafeUtility.SizeOf<T>() * count);
            UnsafeUtility.MemCpy(newKeys, keys, UnsafeUtility.SizeOf<Hash128>() * count);
            UnsafeUtility.MemCpy(newChildren, children, UnsafeUtility.SizeOf<Child>() * count);
            UnsafeUtility.MemCpy(newParents, parents, UnsafeUtility.SizeOf<int>() * count);
            UnsafeUtility.MemCpy(newBalanceFactors, balanceFactors, UnsafeUtility.SizeOf<sbyte>() * count);

            Free(values, allocator);
            Free(keys, allocator);
            Free(children, allocator);
            Free(parents, allocator);
            Free(balanceFactors, allocator);

            values = newValues;
            keys = newKeys;
            children = newChildren;
            parents = newParents;
            balanceFactors = newBalanceFactors;

            capacity = newCapacity;
        }

        public void Dispose()
        {
            if (values != null)
            {
                Free(values, allocator);
                values = null;
            }

            if (keys != null)
            {
                Free(keys, allocator);
                keys = null;
            }

            if (children != null)
            {
                Free(children, allocator);
                children = null;
            }

            if (parents != null)
            {
                Free(parents, allocator);
                parents = null;
            }

            if (balanceFactors != null)
            {
                Free(balanceFactors, allocator);
                balanceFactors = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Insert(Hash128 key, in T value)
        {
            if (root == NULL)
            {
                keys[count] = key;
                values[count] = value;
                balanceFactors[count] = 0;
                children[count] = new Child() { left = NULL, right = NULL };
                SetParent(count, NULL);
                root = count++;
                return true;
            }

            if (count >= capacity)
            {
                Resize(capacity * 2);
            }

            for (int i = root; i != NULL; i = key < keys[i] ? children[i].left : children[i].right)
            {
                if (key < keys[i])
                {
                    if (children[i].left == NULL)
                    {
                        keys[count] = key;
                        values[count] = value;
                        balanceFactors[count] = 0;
                        children[count] = new Child() { left = NULL, right = NULL };
                        SetParent(count, i);
                        children[i].left = count++;
                        InsertBalance(i, 1);
                        return true;
                    }
                }
                else if (key == keys[i])
                {
                    values[i] = value;
                    return true;
                }
                else
                {
                    if (children[i].right == NULL)
                    {
                        keys[count] = key;
                        values[count] = value;
                        balanceFactors[count] = 0;
                        children[count] = new Child() { left = NULL, right = NULL };
                        SetParent(count, i);
                        children[i].right = count++;
                        InsertBalance(i, -1);
                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(Hash128 key, out T value, out int index)
        {
            for (int i = root; i != NULL;)
            {
                if (key < keys[i])
                {
                    i = children[i].left;
                }
                else if (key == keys[i])
                {
                    value = values[i];
                    index = i;
                    return true;
                }
                else
                {
                    i = children[i].right;
                }
            }

            index = -1;
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(Hash128 key)
        {
            if (TryGetValue(key, out _, out int node))
            {
                RemoveAt(node);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveAt(int node)
        {
            int left = children[node].left;
            int right = children[node].right;

            if (left == NULL)
            {
                if (right == NULL)
                {
                    int parent = parents[node];
                    if (parent != NULL)
                    {
                        if (children[parent].left == node)
                        {
                            children[parent].left = NULL;
                            DeleteBalance(parent, -1);
                        }
                        else
                        {
                            children[parent].right = NULL;
                            DeleteBalance(parent, 1);
                        }
                    }
                    else
                    {
                        root = NULL;
                    }
                }
                else
                {
                    int parent = parents[node];
                    if (parent != NULL)
                    {
                        if (children[parent].left == node)
                        {
                            children[parent].left = right;
                        }
                        else
                        {
                            children[parent].right = right;
                        }
                    }
                    else
                    {
                        root = right;
                    }
                    SetParent(right, parent);
                    DeleteBalance(right, 0);
                }
            }
            else if (right == NULL)
            {
                int parent = parents[node];
                if (parent != NULL)
                {
                    if (children[parent].left == node)
                    {
                        children[parent].left = left;
                    }
                    else
                    {
                        children[parent].right = left;
                    }
                }
                else
                {
                    root = left;
                }
                SetParent(left, parent);
                DeleteBalance(left, 0);
            }
            else
            {
                int successor = right;
                if (children[successor].left == NULL)
                {
                    int parent = parents[node];
                    children[successor].left = left;
                    balanceFactors[successor] = balanceFactors[node];
                    SetParent(successor, parent);
                    SetParent(left, successor);

                    if (node == root)
                    {
                        root = successor;
                    }
                    else
                    {
                        if (children[parent].left == node)
                        {
                            children[parent].left = successor;
                        }
                        else
                        {
                            children[parent].right = successor;
                        }
                    }
                    DeleteBalance(successor, 1);
                }
                else
                {
                    while (children[successor].left != NULL)
                    {
                        successor = children[successor].left;
                    }

                    int parent = parents[node];
                    int successorParent = parents[successor];
                    int successorRight = children[successor].right;

                    if (children[successorParent].left == successor)
                    {
                        children[successorParent].left = successorRight;
                    }
                    else
                    {
                        children[successorParent].right = successorRight;
                    }

                    SetParent(successorRight, successorParent);
                    SetParent(successor, parent);
                    SetParent(right, successor);
                    SetParent(left, successor);
                    children[successor].left = left;
                    children[successor].right = right;
                    balanceFactors[successor] = balanceFactors[node];

                    if (node == root)
                    {
                        root = successor;
                    }
                    else
                    {
                        if (children[parent].left == node)
                        {
                            children[parent].left = successor;
                        }
                        else
                        {
                            children[parent].right = successor;
                        }
                    }
                    DeleteBalance(successorParent, -1);
                }
            }

            count--;

            if (node != count)
            {
                int parent = NULL;

                if (root == count)
                {
                    root = node;
                }
                else
                {
                    parent = parents[count];
                    if (children[parent].left == count)
                    {
                        children[parent].left = node;
                    }
                    else
                    {
                        children[parent].right = node;
                    }
                }

                SetParent(children[count].left, node);
                SetParent(children[count].right, node);

                keys[node] = keys[count];
                values[node] = values[count];
                balanceFactors[node] = balanceFactors[count];
                children[node] = children[count];
                SetParent(node, parent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertBalance(int node, sbyte balance)
        {
            while (node != NULL)
            {
                balance = balanceFactors[node] += balance;

                if (balance == 0)
                {
                    return;
                }
                else if (balance == 2)
                {
                    if (balanceFactors[children[node].left] == 1)
                    {
                        RotateRight(node);
                    }
                    else
                    {
                        RotateLeftRight(node);
                    }

                    return;
                }
                else if (balance == -2)
                {
                    if (balanceFactors[children[node].right] == -1)
                    {
                        RotateLeft(node);
                    }
                    else
                    {
                        RotateRightLeft(node);
                    }
                    return;
                }

                int parent = parents[node];

                if (parent != NULL)
                {
                    balance = children[parent].left == node ? (sbyte)1 : (sbyte)-1;
                }

                node = parent;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeleteBalance(int node, sbyte balance)
        {
            while (node != NULL)
            {
                balance = balanceFactors[node] += balance;

                if (balance == -2)
                {
                    if (balanceFactors[children[node].right] <= 0)
                    {
                        node = RotateLeft(node);
                        if (balanceFactors[node] == 1)
                        {
                            return;
                        }
                    }
                    else
                    {
                        node = RotateRightLeft(node);
                    }
                }
                else if (balance == 2)
                {
                    if (balanceFactors[children[node].left] >= 0)
                    {
                        node = RotateRight(node);
                        if (balanceFactors[node] == -1)
                        {
                            return;
                        }
                    }
                    else
                    {
                        node = RotateLeftRight(node);
                    }
                }
                else if (balance != 0)
                {
                    return;
                }

                if (node != NULL)
                {
                    int parent = parents[node];
                    if (parent != NULL)
                    {
                        balance = children[parent].left == node ? (sbyte)-1 : (sbyte)1;
                    }
                    node = parent;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetParent(int node, int parent)
        {
            if (node != NULL)
            {
                parents[node] = parent;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RotateLeft(int node)
        {
            int right = children[node].right;
            int rightLeft = children[right].left;
            int parent = parents[node];

            SetParent(right, parent);
            SetParent(node, right);
            SetParent(rightLeft, node);
            children[right].left = node;
            children[node].right = rightLeft;

            if (node == root)
            {
                root = right;
            }
            else if (children[parent].right == node)
            {
                children[parent].right = right;
            }
            else
            {
                children[parent].left = right;
            }

            balanceFactors[right]++;
            balanceFactors[node] = (sbyte)-balanceFactors[right];

            return right;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RotateRight(int node)
        {
            int left = children[node].left;
            int leftRight = children[left].right;
            int parent = parents[node];

            SetParent(left, parent);
            SetParent(node, left);
            SetParent(leftRight, node);
            children[left].right = node;
            children[node].left = leftRight;

            if (node == root)
            {
                root = left;
            }
            else if (children[parent].left == node)
            {
                children[parent].left = left;
            }
            else
            {
                children[parent].right = left;
            }

            balanceFactors[left]--;
            balanceFactors[node] = (sbyte)-balanceFactors[left];

            return left;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RotateLeftRight(int node)
        {
            int left = children[node].left;
            int leftRight = children[left].right;
            int leftRightRight = children[leftRight].right;
            int leftRightLeft = children[leftRight].left;
            int parent = parents[node];

            SetParent(leftRight, parent);
            SetParent(left, leftRight);
            SetParent(node, leftRight);
            SetParent(leftRightRight, node);
            SetParent(leftRightLeft, left);
            children[node].left = leftRightRight;
            children[left].right = leftRightLeft;
            children[leftRight].left = left;
            children[leftRight].right = node;

            if (node == root)
            {
                root = leftRight;
            }
            else if (children[parent].left == node)
            {
                children[parent].left = leftRight;
            }
            else
            {
                children[parent].right = leftRight;
            }

            if (balanceFactors[leftRight] == 0)
            {
                balanceFactors[node] = 0;
                balanceFactors[left] = 0;
            }
            else if (balanceFactors[leftRight] == -1)
            {
                balanceFactors[node] = 0;
                balanceFactors[left] = 1;
            }
            else
            {
                balanceFactors[node] = -1;
                balanceFactors[left] = 0;
            }
            balanceFactors[leftRight] = 0;

            return leftRight;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int RotateRightLeft(int node)
        {
            int right = children[node].right;
            int rightLeft = children[right].left;
            int rightLeftLeft = children[rightLeft].left;
            int rightLeftRight = children[rightLeft].right;
            int parent = parents[node];

            SetParent(rightLeft, parent);
            SetParent(right, rightLeft);
            SetParent(node, rightLeft);
            SetParent(rightLeftLeft, node);
            SetParent(rightLeftRight, right);
            children[node].right = rightLeftLeft;
            children[right].left = rightLeftRight;
            children[rightLeft].right = right;
            children[rightLeft].left = node;

            if (node == root)
            {
                root = rightLeft;
            }
            else if (children[parent].right == node)
            {
                children[parent].right = rightLeft;
            }
            else
            {
                children[parent].left = rightLeft;
            }

            if (balanceFactors[rightLeft] == 0)
            {
                balanceFactors[node] = 0;
                balanceFactors[right] = 0;
            }
            else if (balanceFactors[rightLeft] == 1)
            {
                balanceFactors[node] = 0;
                balanceFactors[right] = -1;
            }
            else
            {
                balanceFactors[node] = 1;
                balanceFactors[right] = 0;
            }
            balanceFactors[rightLeft] = 0;

            return rightLeft;
        }
    }
}
