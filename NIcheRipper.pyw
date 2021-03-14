"""This is the GUI for the image ripper"""
from datetime import datetime
import threading
import json
import os
import time
import collections
import subprocess
import PySimpleGUI as sg
from rippers import ImageRipper, read_config, write_config, url_check 

# pylint: disable=line-too-long
class RipperGui():
    """GUI Object"""
    def __init__(self):
        self.theme_color = read_config('DEFAULT', 'Theme')
        self.save_folder = read_config('DEFAULT', 'SavePath')
        self.live_history_update = RipperGui.string_to_bool(read_config('DEFAULT', 'LiveHistoryUpdate'))
        self.rerip_ask = RipperGui.string_to_bool(read_config('DEFAULT', 'AskToReRip'))
        self.max_threads = int(read_config('DEFAULT', 'NumberOfThreads'))
        if os.path.isfile('RipHistory.json'):
            self.table_data = self.read_from_file('RipHistory.json')
        else:
            self.table_data = [[" ", " ", " ", " "]]
        self.ripper_list = []
        self.url_list = []
        self.url_list_size = len(self.url_list)
        self.loaded_file = False
        self.latest_version = self.get_git_version()
        self.version = 'v1.6.0'

    def app_gui(self):
        """Run the GUI for the Image Ripper"""
        sg.theme(self.theme_color)   # Add a touch of color
        # Tab layouts.
        logger_layout =  [[sg.Multiline(size=(90,20), key = '-OUTPUT-', echo_stdout_stderr=True, disabled=True, write_only=True, reroute_stderr=True, reroute_stdout=True, autoscroll=True)]]
        headings = ["Name                       ", " URL                      ", "Date        ", "  #  "]
        queue_layout = [[sg.Multiline(size=(90,20), disabled=True, autoscroll=False, key='-QUEUE-', write_only=True)]]
        history_layout = [[sg.Table(size=(90, 20), values=self.table_data, headings=headings, max_col_width=25,
                        auto_size_columns=True, display_row_numbers=False, justification='right', num_rows=9, key='-TABLE-', row_height=35)]]
        settings_layout = [[sg.Text('Save Location: '), sg.Text(text=str(self.save_folder), size=(65, 1),key='-FOLDER-')],
                [sg.Text('Select Save Folder:'), sg.Input(default_text=self.save_folder, key='-SAVEFOLDER-', visible=False, enable_events=True), sg.FolderBrowse(initial_folder=self.save_folder, change_submits=True)],
                [sg.Text('Load Unfinished Urls:'), sg.Input(key='-LOADFILE-', visible=False, enable_events=True), sg.FileBrowse(initial_folder='./', file_types=(('JSON Files', 'UnfinishedRips.json'), ), change_submits=True)],
                [sg.Text('Check for updates: '), sg.Button('Check', enable_events=True), sg.Text(key='-UPDATE-', size=(50, 1))],
                [sg.Text('Change Theme:'), sg.Drop(sg.theme_list(), default_value=self.theme_color, key='-THEME-', enable_events=True)],
                [sg.Check('Ask to re-rip url', key='-RERIP-', default=self.rerip_ask, enable_events=True), sg.Check('Live update history table', key='-LIVEUPDATE-', default=self.live_history_update, enable_events=True)],
                #[sg.Text('Max Number of Threads: '), sg.Spin([i for i in range(1,11)], initial_value=int(self.max_threads), key='-MAXTHREADS-', enable_events=True, size=(3, 1))],
                [sg.Text('Number of threads running: '), sg.Text(key='-THREADS-')]]
        # GUI layout
        layout = [[sg.Text('Enter URL: '), sg.InputText(key='-URL-', do_not_clear=False), sg.Button('Rip', bind_return_key=True), sg.Button('Cancel'), sg.Text(key='-STATUS-', size=(20, 1))],
                [sg.TabGroup([[sg.Tab('Log', logger_layout), sg.Tab('Queue', queue_layout), sg.Tab('History', history_layout), sg.Tab('Settings', settings_layout)]])]]

        # Create the Window
        window = sg.Window(' '.join(['NicheRipper', self.version]), layout)
        #Seperate thread so the queue display can be updated
        checker_thread = threading.Thread(target=self.list_checker, args=(window,), daemon=True)
        # Event Loop to process "events" and get the "values" of the inputs
        while True:
            event, values = window.read()
            if event in (sg.WIN_CLOSED, 'Cancel'): #Save files when gui is closed
                self.close_program()
                break
            if event == 'Rip': #Image rip behavior
                if url_check(values['-URL-']) and not values['-URL-'] in self.url_list: #If url is for a supported site and not already queued
                    if self.rerip_ask and any(values['-URL-'] in sublist for sublist in self.table_data): #If user wants to be prompted and if url is in the history
                        if sg.popup_yes_no('Do you want to re-rip URL?', no_titlebar=True) == 'Yes': #Ask user to re-rip
                            self.url_list.append(values['-URL-'])
                    else: #If user always wants to re-rip
                        self.url_list.append(values['-URL-'])
                elif values['-URL-'] in self.url_list: #If url is already queued
                    window['-STATUS-']('Already queued', text_color='green')
                elif values['-URL-'] == "" and self.url_list: #If not url is entered but there are queued urls (loading from UnfinishedRips.json)
                    window['-STATUS-']('Starting rip', text_color='green')
                elif values['-URL-'] == "" and not self.url_list: #If no input and no queue 
                    window['-STATUS-']('')
                else: #If the url is not supported
                    window['-STATUS-']('Not a supported site', text_color='red')
                if not checker_thread.is_alive() and self.url_list: #If thread is not running and there are queued urls
                    checker_thread = threading.Thread(target=self.list_checker, args=(window,), daemon=True)
                    checker_thread.start()
                self.print_queue(window)
            if event == 'Check': #Check if there is an update available
                if self.version >= self.latest_version:
                    window['-UPDATE-'](' '.join([self.version, 'is the latest version']), text_color='green')
                else:
                    window['-UPDATE-']('Update available', text_color='red')
            if values['-LOADFILE-'] and not self.loaded_file: #Load unfinished urls once
                unfinished_list = self.read_from_file(values['-LOADFILE-'])
                #for url in unfinished_list:
                 #   if url_check(url):
                  #      self.url_list.append(url)
                self.url_list.extend(unfinished_list)
                self.loaded_file = True
                os.remove(values['-LOADFILE-'])
            self.live_history_update = values['-LIVEUPDATE-']
            self.rerip_ask = values['-RERIP-']
            self.save_folder = values['-SAVEFOLDER-']
            #self.max_threads = int(values['-MAXTHREADS-'])
            if not self.save_folder[-1] == '/': #Makes sure the save path ends with '/'
                self.save_folder += '/'
            window['-FOLDER-'].update(self.save_folder)
            window['-THREADS-'].update(threading.active_count())
            time.sleep(0.2)

        window.close()

    def close_program(self):
        """Saves all the necessary information"""
        RipperGui.save_to_file('RipHistory.json', self.table_data) #Save history data
        if self.url_list:
            RipperGui.save_to_file('UnfinishedRips.json', self.url_list) #Save queued urls
        write_config('DEFAULT', 'SavePath', self.save_folder) #Update the config
        write_config('DEFAULT', 'Theme', self.theme_color)
        write_config('DEFAULT', 'AskToReRip', str(self.rerip_ask))
        write_config('DEFAULT', 'LiveHistoryUpdate', str(self.live_history_update))
        write_config('DEFAULT', 'NumberOfThreads', str(self.max_threads))

    def list_checker(self, window: sg.Window):
        """Run the ripper thread if the url list is not empty"""
        ripper = threading.Thread(target=self.rip_images, args=(window,), daemon=True)
        while self.url_list:
            if not ripper.is_alive():
                ripper = threading.Thread(target=self.rip_images, args=(window,), daemon=True)
                ripper.start()
                time.sleep(2)

    def rip_images(self, window: sg.Window):
        """Rips files from url"""
        url = self.url_list[0]
        print(url)
        img_ripper = ImageRipper(url) # pylint: disable=not-callable
        img_ripper.image_getter()
        self.update_table(img_ripper, url, self.live_history_update, window)
        self.url_list.remove(url)
        self.print_queue(window)

    def print_queue(self, window: sg.Window):
        """Update the displayed queue"""
        if len(self.url_list) != self.url_list_size: #If the url queue changes size
            window['-QUEUE-']('') #Clears the queue
            for url in self.url_list: #Re-prints the queue #Change this to not use range(len())
                window.find_element('-QUEUE-').print(url)
            self.url_list_size = len(self.url_list)

    def update_table(self, ripper: ImageRipper, url: str, update: bool, window: sg.Window):
        """Update the table with new values"""
        if self.table_data[0][0] == " ": #If the first value in the table empty
            del self.table_data[0] #Replace with real table value
        duplicate_entry = False
        for index in range(len(self.table_data)):
            if self.table_data[index][0] == ripper.folder_info[2]:
                duplicate_entry = True
                self.table_data[index][2] = str(datetime.today().strftime('%Y-%m-%d'))
                self.table_data.append(self.table_data[index])
                del self.table_data[index]
                break
        if not duplicate_entry:
            self.table_data.append([ripper.folder_info[2], url, str(datetime.today().strftime('%Y-%m-%d')), str(ripper.folder_info[1])])
        ripper.folder_info = []
        if update:
            window['-TABLE-'].update(values=self.table_data)

    @staticmethod
    def string_to_bool(v: str) -> bool:
        """Convert string to boolean value"""
        return v.lower() in ("yes", "true", "t", "1")

    @staticmethod
    def save_to_file(file_name: str, data: any):
        """Save data to file"""
        with open(file_name, 'w+') as save_file:
            if isinstance(data, collections.deque):
                data = list(data)
            json.dump(data, save_file)

    @staticmethod
    def read_from_file(file_name: str) -> any:
        """Read data from file"""
        try:
            with open(file_name, 'r') as load_file:
                data = json.load(load_file)
                return data
        except FileNotFoundError:
            pass

    @staticmethod
    def get_git_version() -> str:
        version = subprocess.check_output(['git', 'describe', '--tags'])
        version = version.decode("utf-8").strip('\n')
        end = version.find('-', 0)
        version = version[0:end]
        return version

if __name__ == "__main__":
    rip_gui = RipperGui()
    rip_gui.app_gui()
    