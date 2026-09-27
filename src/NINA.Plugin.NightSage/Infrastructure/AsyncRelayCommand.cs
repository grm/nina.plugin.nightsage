using System.Windows.Input;

namespace NINA.Plugin.NightSage.Infrastructure;

public sealed class AsyncRelayCommand : ICommand {
    private readonly Func<Task> execute;
    private readonly Func<bool>? canExecute;
    private bool running;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) {
        if (!CanExecute(parameter)) return;
        running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute().ConfigureAwait(true); }
        finally {
            running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
