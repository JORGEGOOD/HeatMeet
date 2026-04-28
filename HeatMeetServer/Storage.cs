namespace HeatMeetServer
{
    internal class Storage
    {
        //Probably there's not enoughg time for this

        //### FOLDERS ###
        //
        //    Files
        //      |
        //     / \
        //    /   \
        //   /     \
        // Group  Chat
        //
        //###############
        private static string rootFolder        = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files"      );
        private static string groupImagesFolder = Path.Combine(rootFolder                           , "GroupImages");
        private static string chatImagesFolder  = Path.Combine(rootFolder                           , "ChatImages" );

        public static void Initialize() //Check if 3 folders exists
        {
            try
            {
                //if "Files"       exists
                if(!Directory.Exists(rootFolder))       Directory.CreateDirectory(rootFolder);

                //if "GroupImages" exists
                if(!Directory.Exists(groupImagesFolder)) Directory.CreateDirectory(groupImagesFolder);
                
                //if "ChatImages" exists
                if(!Directory.Exists(chatImagesFolder))  Directory.CreateDirectory(chatImagesFolder);
            }
            catch (Exception ex)
            {
                Log.Add_Log($"[STORAGE ERROR] Folders couldn't be created : {ex.Message}");
            }
        }

        //Returns path of file in string
        public static string GetGroupImagePath(string fileName)
        {
            return Path.Combine(groupImagesFolder, fileName);
        }

        //Returns path of file in string
        public static string GetChatImagePath(string fileName)
        {
            return Path.Combine(chatImagesFolder, fileName);
        }
    }
}
