"""This is the GUI for the image ripper"""
from datetime import datetime
import threading
import json
import os
import time
import collections
import PySimpleGUI as sg
from ImageRipper import ImageRipper, read_config, write_config, url_check 

# pylint: disable=line-too-long
class RipperGui():
    """GUI Object"""
    def __init__(self):
        self.theme_color = read_config('DEFAULT', 'Theme')
        self.save_folder = read_config('DEFAULT', 'SavePath')
        self.rerip_ask = read_config('DEFAULT', 'AskToReRip')
        self.table_data = [[" ", " ", " ", " "]]
        if os.path.isfile('RipHistory.json'):
            self.table_data = self.read_from_file('RipHistory.json')
        self.url_queue = collections.deque()
        self.url_queue_size = len(self.url_queue)

    def app_gui(self):
        """Run the GUI for the Image Ripper"""
        sg.theme(self.theme_color)   # Add a touch of color
        # All the stuff inside your window.
        logger_layout =  [[sg.Multiline(size=(90,20), key = '-OUTPUT-', echo_stdout_stderr=True, disabled=True, write_only=True, reroute_stdout=True)]]
        headings = ["Name                       ", " URL                      ", "Date        ", "  #  "]
        queue_layout = [[sg.Multiline(size=(90,20), disabled=True, autoscroll=False, key='-QUEUE-', write_only=True)]]
        history_layout = [[sg.Table(size=(90, 20), values=self.table_data, headings=headings, max_col_width=25,
                        auto_size_columns=True,
                        display_row_numbers=False,
                        justification='right',
                        num_rows=9,
                        key='-TABLE-',
                        row_height=35)]]
        settings_layout = [[sg.Text('Save Location: '), sg.Text(text=str(self.save_folder), size=(65, 1),key='-FOLDER-')],
                [sg.Text('Select Save Folder'), sg.Input(default_text=self.save_folder, key='-SAVEFOLDER-', visible=False, enable_events=True), sg.FolderBrowse(initial_folder=self.save_folder, change_submits=True)],
                [sg.Text('Change Theme'), sg.Drop(sg.theme_list(), default_value=self.theme_color, key='-THEME-', enable_events=True)],
                [sg.Check('Ask to re-rip url', key='-RERIP-', default=bool(self.rerip_ask), enable_events=True)],
                [sg.Text('Number of threads running: '), sg.Text(key='-THREADS-')]]
        layout = [[sg.T('Enter URL: '), sg.InputText(key='-URL-', do_not_clear=False), sg.Button('Rip', change_submits=True, enable_events=True, bind_return_key=True), sg.Button('Cancel'), sg.T(key='-STATUS-', size=(20, 1))],
                [sg.TabGroup([[sg.Tab('Log', logger_layout), sg.Tab('Queue', queue_layout), sg.Tab('History', history_layout), sg.Tab('Settings', settings_layout)]])]]

        # Create the Window
        window = sg.Window('Image Ripper v1.0.0', layout)
        #Seperate thread so the queue display can be updated
        checker_thread = threading.Thread(target=self.list_checker, args=(window,), daemon=True)
        # Event Loop to process "events" and get the "values" of the inputs
        while True:
            event, values = window.read()
            if event in (sg.WIN_CLOSED, 'Cancel'): #Save files when gui is closed
                self.close_program()
                break
            if event == 'Rip': #Image rip behavior
                if url_check(values['-URL-']) and not values['-URL-'] in self.url_queue: #If url is for a supported site and not already queued
                    window['-STATUS-']('')
                    if bool(self.rerip_ask) and any(values['-URL-'] in sublist for sublist in self.table_data): #If user wants to be prompted and if url is in the history
                        if sg.popup_yes_no('Do you want to re-rip URL?', no_titlebar=True) == 'Yes': #Ask user to re-rip
                            self.url_queue.append(values['-URL-'])
                    else: #If user always wants to re-rip
                        self.url_queue.append(values['-URL-'])
                elif values['-URL-'] in self.url_queue: #If url is already queued
                    window['-STATUS-']('Already queued', text_color='green')
                else: #If the url is not supported
                    window['-STATUS-']('Not a supported site', text_color='red')
                if not checker_thread.is_alive() and self.url_queue: #If thread is not running and there are queued urls
                    checker_thread = threading.Thread(target=self.list_checker, args=(window,), daemon=True)
                    checker_thread.start()
                self.print_queue(window)
            self.rerip_ask = values['-RERIP-']
            self.save_folder = values['-SAVEFOLDER-']
            if not self.save_folder[-1] == '/': #Makes sure the save path ends with '/'
                self.save_folder += '/'
            window['-FOLDER-'].update(self.save_folder)
            time.sleep(0.2)

        window.close()

    def close_program(self):
        """Saves all the necessary information"""
        RipperGui.save_to_file('RipHistory.json', self.table_data) #Save history data
        if self.url_queue:
            RipperGui.save_to_file('UnfinishedRips.json', self.url_queue) #Save queued urls
        write_config('DEFAULT', 'SavePath', self.save_folder) #Update the config
        write_config('DEFAULT', 'Theme', self.theme_color)
        write_config('DEFAULT', 'AskToReRip', str(self.rerip_ask))

    def list_checker(self, window):
        """Run the ripper thread if the url list is not empty"""
        ripper = threading.Thread(target=self.rip_images, args=(window,), daemon=True)
        window['-THREADS-'].update(threading.active_count())
        while self.url_queue:
            if not ripper.is_alive():
                ripper = threading.Thread(target=self.rip_images, args=(window,), daemon=True)
                ripper.start()

    def rip_images(self, window):
        """Rips files from url"""
        if self.url_queue:
            print(self.url_queue[0])
            img_ripper = ImageRipper(self.url_queue[0]) # pylint: disable=not-callable
            img_ripper.image_getter()
            self.update_table(img_ripper, window)
            self.url_queue.popleft()
            self.print_queue(window)

    def print_queue(self, window):
        """Update the displayed queue"""
        if len(self.url_queue) != self.url_queue_size: #If the url queue changes size
            window['-QUEUE-']('') #Clears the queue
            for url in self.url_queue: #Re-prints the queue #Change this to not use range(len())
                window.find_element('-QUEUE-').print(url)
            self.url_queue_size = len(self.url_queue)

    def update_table(self, ripper, window):
        """Update the table with new values"""
        if self.table_data[0][0] == " ": #If the first value in the table empty
            del self.table_data[0] #Replace with real table value
        duplicate_entry = False
        for index in range(len(self.table_data)): # pylint: disable=consider-using-enumerate #use something else
            if self.table_data[index][0] == ripper.folder_info[2]:
                duplicate_entry = True
                self.table_data[index][2] = str(datetime.today().strftime('%Y-%m-%d'))
                self.table_data.append(self.table_data[index])
                del self.table_data[index]
                return
        if not duplicate_entry:
            self.table_data.append([ripper.folder_info[2], self.url_queue[0], str(datetime.today().strftime('%Y-%m-%d')), str(ripper.folder_info[1])])
        ripper.folder_info = []
        window['-TABLE-'].update(values=self.table_data)

    @staticmethod
    def save_to_file(file_name, data):
        """Save data to file"""
        with open(file_name, 'w+') as save_file:
            if isinstance(data, collections.deque):
                data = list(data)
            json.dump(data, save_file)

    @staticmethod
    def read_from_file(file_name):
        """Read data from file"""
        with open(file_name, 'r') as load_file:
            data = json.load(load_file)
            return data

if __name__ == "__main__":
    rip_gui = RipperGui()
    rip_gui.app_gui()
