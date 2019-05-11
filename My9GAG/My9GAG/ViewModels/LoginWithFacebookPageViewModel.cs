﻿using My9GAG.Logic.Client;
using My9GAG.Logic.FacebookAuthentication;
using My9GAG.Logic.PageNavigator;
using My9GAG.Models.Request;
using System.Windows.Input;
using Xamarin.Forms;

namespace My9GAG.ViewModels
{
    public class LoginWithFacebookPageViewModel : ViewModelBase
    {
        #region Constructors

        public LoginWithFacebookPageViewModel(
            IFacebookAuthenticationService facebookAuthenticationService,
            IClientService clientService,
            IPageNavigator pageNavigator)
        {
            _facebookAuthenticationService = facebookAuthenticationService;
            _clientService = clientService;
            _pageNavigator = pageNavigator;

            InitCommands();

            _state = RequestUtils.GetUuid();
            PageUrl = _facebookAuthenticationService.GetAuthenticationPageUrl(_state);
        }

        #endregion

        #region Properties

        public string PageUrl
        {
            get { return _pageUrl; }
            set { SetProperty(ref _pageUrl, value); }
        }

        #endregion

        #region Methods

        //public async void LoginAsync(string code)
        //{
        //    StartWorkIndication(ViewModelConstants.LOGIN_MESSAGE);

        //    await Task.Run(async () =>
        //    {
        //        string accessToken = await _googleAuthenticationService.GetAccessTokenAsync(code);

        //        if (!string.IsNullOrWhiteSpace(accessToken))
        //        {
        //            var requestStatus = await _clientService.LoginWithGoogleAsync(accessToken);

        //            if (requestStatus.IsSuccessful)
        //            {
        //                await Task.Delay(ViewModelConstants.LOGIN_DELAY);

        //                Device.BeginInvokeOnMainThread(() =>
        //                {
        //                    _pageNavigator.OpenPostsPage();
        //                });
        //            }
        //            else
        //            {
        //                await ShowMessage(ViewModelConstants.LOGIN_FAILED_MESSAGE, ViewModelConstants.MESSAGE_DELAY);

        //                Device.BeginInvokeOnMainThread(() =>
        //                {
        //                    _pageNavigator.GoBack();
        //                });
        //            }
        //        }
        //        else
        //        {
        //            await ShowMessage(ViewModelConstants.LOGIN_WITH_GOOGLE_FAILED_MESSAGE, ViewModelConstants.MESSAGE_DELAY);

        //            Device.BeginInvokeOnMainThread(() =>
        //            {
        //                _pageNavigator.GoBack();
        //            });
        //        }
        //    });

        //    StopWorkIndication();
        //}

        #endregion

        #region Commands

        public ICommand NavigatingCommand
        {
            get;
            protected set;
        }

        #endregion

        #region Implementation

        private void InitCommands()
        {
            NavigatingCommand = new Command(
                redirectUrl => { }/*LoginAsync((string)redirectUrl)*/);
        }

        #endregion

        #region Fields

        private IFacebookAuthenticationService _facebookAuthenticationService;
        private IClientService _clientService;
        private IPageNavigator _pageNavigator;
        private string _pageUrl;
        private string _state;

        #endregion
    }
}
