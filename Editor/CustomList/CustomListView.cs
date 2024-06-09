using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scellecs.Morpeh.Graphics.Editor
{
    public sealed class CustomListView<T> : VisualElement
    {
        private List<T> list;
        private Func<IEnumerable<DropdownItem<T>>> selector;
        private IEqualityComparer<T> comparer;
        private bool isUniqueList;

        private Foldout baseFoldout;
        private Button addButton;
        private Button removeButton;
        private ListView listView;

        private StyleSheet baseStyleSheet;
        private SerializedProperty property;

        public CustomListView(SerializedProperty property, Func<IEnumerable<DropdownItem<T>>> selector)
        {
            this.property = property;
            this.selector = selector;
            isUniqueList = false;

            Init();
        }

        public CustomListView(SerializedProperty property, Func<IEnumerable<DropdownItem<T>>> selector, IEqualityComparer<T> comparer, bool isUniqueList)
        {
            this.property = property;
            this.selector = selector;
            this.comparer = comparer;
            this.isUniqueList = isUniqueList;

            Init();
        }

        private void Init()
        {
            list = new List<T>();
            baseStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath("05c7afd38c47b9b439401fc4a8994bb2"));

            var root = new VisualElement();
            root.styleSheets.Add(baseStyleSheet);

            CreateBaseFoldout();
            CreateAddButton();
            CreateRemoveButton();
            CreateListView(property);
            CheckCollapseList();

            root.Add(baseFoldout);
            root.Add(addButton);
            root.Add(removeButton);
            baseFoldout.Add(listView);
            hierarchy.Add(root);
        }

        private void CreateBaseFoldout()
        {
            baseFoldout = new Foldout();
            baseFoldout.AddToClassList("custom-list-base-foldout");
            baseFoldout.text = property.displayName;
        }

        private void CreateAddButton()
        {
            addButton = new Button() { text = "+" };
            addButton.AddToClassList("custom-list-add-button");
            addButton.clicked += () =>
            {
                CreateDropdownMenu(addButton);
            };
        }

        private void CreateRemoveButton()
        {
            removeButton = new Button() { text = "-" };
            removeButton.AddToClassList("custom-list-remove-button");
            removeButton.clicked += () =>
            {
                var index = listView.selectedIndex >= 0 ? listView.selectedIndex : 0;
                RemoveFromList(index);
            };
        }

        private void CreateListView(SerializedProperty property)
        {
            listView = new ListView();
            listView.AddToClassList("custom-list-list-view");

            listView.reorderable = false;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            listView.showAddRemoveFooter = false;
            listView.fixedItemHeight = 20f;
            listView.showBorder = false;
            listView.showBoundCollectionSize = false;
            listView.showFoldoutHeader = false;
            listView.bindItem = (e, i) =>
            {
                var prop = e as PropertyField;
                var arrayProp = property.GetArrayElementAtIndex(i);
                prop.BindProperty(arrayProp);
            };
            listView.BindProperty(property);
            CopyToList();
        }

        private void CheckCollapseList()
        {
            if (list.Count == 0)
            {
                baseFoldout.value = false;
            }
        }

        private void CopyToList()
        {
            list.Clear();

            if (property != null)
            {
                for (int i = 0; i < property.arraySize; i++)
                {
                    list.Add((T)property.GetArrayElementAtIndex(i).boxedValue);
                }
            }
        }

        private void ExportList()
        {
            property.ClearArray();
            property.arraySize = list.Count;

            for (int i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).boxedValue = list[i];
            }

            property.serializedObject.ApplyModifiedProperties();
        }

        private void AddToList(object item)
        {
            CopyToList();
            list.Add((T)item);
            ExportList();
            baseFoldout.value = true;
        }

        private void RemoveFromList(int index)
        {
            if (list.Count > 0)
            {
                CopyToList();
                list.RemoveAt(index);
                ExportList();

                if (list.Count == 0)
                {
                    baseFoldout.value = false;
                }
            }
        }

        private void CreateDropdownMenu(VisualElement element)
        {
            var menu =  new GenericDropdownMenu();
            var items = selector?.Invoke();
            if (isUniqueList)
            {
                items = items.Where(dropdownItem => !list.Contains(dropdownItem.value, comparer)).ToList();
            }

            if (items != null && items.Count() > 0)
            {
                foreach (var item in items)
                {
                    menu.AddItem(item.name, false, AddToList, item.value);
                }
            }
            else
            {
                menu.AddDisabledItem("There are no possible items to add", false);
            }

            menu.DropDown(element.worldBound, element);
        }
    }

    public struct DropdownItem<T>
    {
        public T value;
        public string name;
    }
}
