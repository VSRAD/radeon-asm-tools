﻿using System.Collections.ObjectModel;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public class NavigationListNode
    {
        public string Text { get; private set; }
        public ObservableCollection<NavigationListNode> Items { get; private set; }

        public NavigationListNode(string name)
        {
            Text = name;
            Items = new ObservableCollection<NavigationListNode>();
        }
    }

    public class NavigationListItemNode : NavigationListNode
    {
        public NavigationToken NavigationToken { get; }
        public NavigationListItemNode(NavigationToken navigationToken) 
            : base($"{navigationToken.Line + 1}: {navigationToken.LineText}")
        {
            NavigationToken = navigationToken;
        }
    }
}
