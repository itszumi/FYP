// PlayerSession.cs
// Static class that persists the currently logged-in username across scene loads.
// Call PlayerSession.SetUser() after a successful login.

public static class PlayerSession
{
    private static string _currentUser = "";
    private static int    _selectedAvatar = 0;

    public static void SetUser(string username, int avatarIndex = 0)
    {
        _currentUser    = username;
        _selectedAvatar = avatarIndex;
    }

    public static string GetUser()         => _currentUser;
    public static int    GetAvatarIndex()  => _selectedAvatar;
    public static bool   IsLoggedIn()      => !string.IsNullOrEmpty(_currentUser);

    public static void Clear()
    {
        _currentUser    = "";
        _selectedAvatar = 0;
    }
}
