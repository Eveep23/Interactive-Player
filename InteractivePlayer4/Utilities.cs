using System;
using System.IO;
using System.Linq;

public static class Utilities
{
    public static string ShowMovieSelectionMenu()
    {
        string[] movieFolders = Directory.GetDirectories(Directory.GetCurrentDirectory());
        movieFolders = movieFolders.Where(folder =>
            !Path.GetFileName(folder).Equals("libvlc", StringComparison.OrdinalIgnoreCase) &&
            Directory.GetFiles(folder, "*.mkv").Any() &&
            Directory.GetFiles(folder, "*.json").Any()).ToArray();

        if (movieFolders.Length == 0)
        {
            Console.WriteLine("No movies found.");
            return null;
        }

        Console.WriteLine("Available Movies:");
        for (int i = 0; i < movieFolders.Length; i++)
        {
            Console.WriteLine($"{i + 1}: {Path.GetFileName(movieFolders[i])}");
        }

        Console.Write("Enter the number of the movie you want to play: ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= movieFolders.Length)
        {
            return movieFolders[choice - 1];
        }

        Console.WriteLine("Invalid choice.");
        return null;
    }
}
