﻿using My9GAG.ViewModels;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace My9GAG.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class PostsPage : ContentPage
	{
		public PostsPage(PostsPageViewModel viewModel)
		{
			InitializeComponent();
            BindingContext = viewModel;
        }
    }
}