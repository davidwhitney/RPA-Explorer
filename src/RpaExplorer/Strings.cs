using System.Collections.Generic;

namespace RpaExplorer
{
    // Replaces the original Lang.resx / ComponentResourceManager approach.
    // Only English was enabled in the original project; the strings are ported verbatim.
    // Keyed by the abbreviation used by Settings (e.g. "EN") so additional languages
    // can be added later without touching call sites.
    public static class Strings
    {
        private static readonly Dictionary<string, string> EN = new()
        {
            ["Invalid_values"] = "Invalid values",
            ["Exporting_file"] = "Exporting file: ",
            ["Ready"] = "Ready",
            ["Load_RenPy_Archive"] = "Load RenPy Archive",
            ["RPA_RPI_files"] = "RPA/RPI files",
            ["Save_RenPy_Archive"] = "Save RenPy Archive",
            ["Usage_instructions_loaded"] = "Choose file from file list on the side to preview contents. Check and export to save it locally or delete it from archive. Drag and drop files to file list to add new files into archive.",
            ["Preview_is_not_supported"] = "Preview is not supported for selected file/folder.",
            ["Play"] = "Play",
            ["Pause"] = "Pause",
            ["Empty_archive_save"] = "Archive does not contain any files, cannot save empty archive.",
            ["Empty_archive"] = "Empty archive",
            ["Saving_archive"] = "Saving archive...",
            ["Save_failed_reason"] = "Saving new archive failed with following error: {0}",
            ["Save_failed"] = "Save failed",
            ["Archive_modified"] = "Archive modified",
            ["Loading_file"] = "Loading file: ",
            ["Archive_modified_load"] = "Archive was modified, do you really want to load a new one and lose changes?",
            ["Archive_modified_close"] = "Archive was modified, do you really want to exit without saving and lose changes?",
            ["Archive_modified_new"] = "Archive was modified, do you really want to create a new one and lose changes?",
            ["Archive_version"] = "Archive version: ",
            ["Archive_file_location"] = "Archive location: ",
            ["Archive_file_size"] = "Archive size: ",
            ["Index_file_location"] = "Index file location: ",
            ["Index_file_size"] = "Index file size: ",
            ["Files_count"] = "Files count: ",
            ["Unsaved_files_count"] = "Unsaved files count: ",
            ["Selected_file_size"] = "Selected file size: ",
            ["Explorer_title"] = "RenPy Archive Explorer",
            ["Archive_save_title"] = "Archive options",
            ["Archive_save_version"] = "Archive version:",
            ["Archive_save_padding"] = "Data padding:",
            ["Archive_save_obfuscationkey"] = "Obfuscation key:",
            ["Archive_save_continue"] = "Continue",
            ["Archive_save_cancel"] = "Cancel",
            ["Load_file"] = "Load file",
            ["Export_checked"] = "Export checked",
            ["Cancel_operation"] = "Cancel operation",
            ["None"] = "None",
            ["Usage_instructions_new"] = "Start by creating/loading archive file or drag and drop archive file into this area to load it.",
            ["Text"] = "Text",
            ["Media"] = "Media",
            ["Create_new_archive"] = "Create new archive",
            ["File_list"] = "File list:",
            ["Remove_checked"] = "Remove checked",
            ["Save_archive"] = "Save archive",
            ["Image"] = "Image",
            ["Language"] = "Language:",
            ["Replace_file"] = "File '{0}' exists in archive, do you want to replace it?",
            ["File_exists"] = "File exists in archive",
            ["Load_failed_reason"] = "Loading archive failed with following error: {0}",
            ["Load_failed"] = "Archive loading failed",
            ["Selected_file_path"] = "Selected file path: ",
            ["Not_valid_archive_file"] = "Selected file is not valid archive file.",
            ["Search_next"] = "Search next",
            ["Search"] = "Search",
            ["Options"] = "Options",
            ["File_association"] = "Associate RPA/RPI extensions",
            ["About"] = "About",
            ["About_text"] = "RPA Explorer v{0}\nCreated by {1}\nGitHub: {2}\n\nInspired by rpatool and unrpyc.\n\nTranslations credits:\n{3}\n\nContributors credits:\n{4}",
            ["UNRPYC_script"] = "unrpyc script",
            ["Locate_unrpyc_script"] = "Locate unrpyc script",
            ["Python_interpreter"] = "Python interpreter",
            ["Locate_Python_Interpreter"] = "Locate Python interpreter",
            ["Locate_unrpyc"] = "Define unrpyc location",
            ["Download_unrpyc"] = "Download unrpyc",
            ["Vlc_required"] = "VLC required",
            ["Vlc_required_prompt"] = "Audio and video preview is powered by VLC, which does not appear to be installed.\n\nOpen the VLC download page now?",
            ["Vlc_required_prompt_brew"] = "Audio and video preview is powered by VLC, which does not appear to be installed.\n\nYou can install it with Homebrew:\n\n    brew install --cask vlc\n\nOpen the VLC download page instead?",
            ["Vlc_not_installed_hint"] = "Audio/video preview requires VLC. Install it, then select this file again.",
            ["Download_unrpyc_prompt"] = "unrpyc is the external decompiler used to preview compiled Ren'Py scripts.\n\nDownload unrpyc v{0} from GitHub now?\n\n{1}\n\nIt will be stored in:\n{2}",
            ["Downloading_unrpyc"] = "Downloading unrpyc...",
            ["Unrpyc_ready"] = "unrpyc v{0} is ready and has been selected:\n\n{1}\n\nIt requires Python {2} or newer.",
            ["Unrpyc_download_failed"] = "Downloading unrpyc failed: {0}",
            ["Locate_python"] = "Define Python location",
            ["Preview_failed"] = "Preview load failed",
            ["Preview_failed_reason"] = "Loading preview failed with following error: {0}",
            ["Preview_failed_reason_hint"] = "Loading preview failed with error: {0} Use 'Options' to define external locations for this preview.",
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Languages = new()
        {
            ["EN"] = EN,
        };

        public static string Get(string abbrev, string name)
        {
            if (Languages.TryGetValue(abbrev, out var table)
                && table.TryGetValue(name, out var value))
            {
                return value;
            }

            return " {!!! MISSING TRANSLATION !!!} ";
        }
    }
}
