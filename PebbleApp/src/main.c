#include <pebble.h>



#define KEY_SENSOR_DATA 1
#define KEY_FREQUENCY_DATA 2
#define KEY_COMMAND_DATA 3



const uint8_t SAMPLES_PER_UPDATE = 3;

const uint8_t UP_CLICK = 1;
const uint8_t UP_DOUBLE_CLICK = 2;
const uint8_t UP_LONG_CLICK_START = 3;
const uint8_t UP_LONG_CLICK_STOP = 4;

const uint8_t DOWN_CLICK = 5;
const uint8_t DOWN_DOUBLE_CLICK = 6;
const uint8_t DOWN_LONG_CLICK_START = 7;
const uint8_t DOWN_LONG_CLICK_STOP = 8;



static Window *window_main;
static SimpleMenuLayer *main_window_menu_layer;
static SimpleMenuSection main_menu_sections[1];
static SimpleMenuItem main_menu_items[3];

static Window *window_freq;
static SimpleMenuLayer *freq_window_menu_layer;
static SimpleMenuSection freq_menu_sections[1];
static SimpleMenuItem freq_menu_items[4];

static Window *window_bt;
static SimpleMenuLayer *bt_window_menu_layer;
static SimpleMenuSection bt_menu_sections[1];
static SimpleMenuItem bt_menu_items[2];

static Window *window_sensor;
static TextLayer *up_button_text_layer, *down_button_text_layer, *status_text_layer;

AccelSamplingRate accel_sampling_rate;
SniffInterval sniff_interval;

bool new_command_issued;
uint8_t new_command;
uint8_t *new_sensor_data;
uint8_t new_sensor_data_count;



static void sensor_data_handler(AccelData *data, uint32_t num_samples) {
	uint8_t *ptr = new_sensor_data;
	new_sensor_data_count = 0;

	for (uint32_t i = 0; i < num_samples; ++i)
		if (!data[i].did_vibrate) {
			//NOTE: This platform is Little-Endian architechture, same with Windows			
			memcpy(ptr, data + i, sizeof(int16_t) * 3);
			ptr += 6;
			
			++new_sensor_data_count;
		}
}



static void send_command_data() {
	DictionaryIterator *it;

	app_message_outbox_begin(&it);
	dict_write_uint8(it, KEY_COMMAND_DATA, new_command);
	app_message_outbox_send();
}



static void send_freq_data() {
	DictionaryIterator *it;

	app_message_outbox_begin(&it);

	switch (accel_sampling_rate) {
		case ACCEL_SAMPLING_10HZ:
			dict_write_uint8(it, KEY_FREQUENCY_DATA, 10);
			break;
		case ACCEL_SAMPLING_25HZ:
			dict_write_uint8(it, KEY_FREQUENCY_DATA, 25);
			break;
		case ACCEL_SAMPLING_50HZ:
			dict_write_uint8(it, KEY_FREQUENCY_DATA, 50);
			break;
		case ACCEL_SAMPLING_100HZ:
			dict_write_uint8(it, KEY_FREQUENCY_DATA, 100);
			break;			
	}	

	app_message_outbox_send();	
}



static void send_sensor_data() {
	DictionaryIterator *it;

	app_message_outbox_begin(&it);

	dict_write_data(
		it,
		KEY_SENSOR_DATA,
		new_sensor_data,
		sizeof(uint16_t) * 3 * new_sensor_data_count
	);

	app_message_outbox_send();
}



static void log_sent_data(DictionaryIterator *iterator) {
	APP_LOG(APP_LOG_LEVEL_INFO, "Data sent:");

	Tuple *t = dict_read_first(iterator);

	int len = (t->length) / 6;
	int16_t *sent_data = (int16_t*)t->value->data;
	int16_t x,y,z;	

	while (t != NULL) {
		switch (t->key) {
			case KEY_SENSOR_DATA:
				for (int i = 0; i < len; ++i) {
					x = *sent_data;
					++sent_data;

					y = *sent_data;
					++sent_data;

					z = *sent_data;
					++sent_data;

					APP_LOG(APP_LOG_LEVEL_INFO, "   %d,%d,%d\n", x, y, z);
				}

				break;
			case KEY_FREQUENCY_DATA:
				APP_LOG(APP_LOG_LEVEL_INFO, "   Sampling Frequency = %u", t->value->uint8);
				
				break;
			case KEY_COMMAND_DATA:
				APP_LOG(APP_LOG_LEVEL_INFO, "   Command = %u", t->value->uint8);
		}
		
		t = dict_read_next(iterator);
	}	
}



static void outbox_failed_callback(DictionaryIterator *iterator, AppMessageResult reason, void *context) {
	text_layer_set_text(status_text_layer, "Disconnected");
}



static void outbox_sent_callback(DictionaryIterator *iterator, void *context) {
	if (new_command_issued) {
		new_command_issued = false;
		send_command_data();
	} else
		send_sensor_data();
	
	log_sent_data(iterator);
}



void up_click_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = UP_CLICK;

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Up Click");
}



void up_double_click_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = UP_DOUBLE_CLICK;	

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Up Double Click");
}



void up_long_click_start_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = UP_LONG_CLICK_START;	

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Up Long Click Start");
}



void up_long_click_stop_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = UP_LONG_CLICK_STOP;	

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Up Long Click Stop");
}



void down_click_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = DOWN_CLICK;

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Down Click");
}



void down_double_click_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = DOWN_DOUBLE_CLICK;	

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Down Double Click");
}



void down_long_click_start_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = DOWN_LONG_CLICK_START;	

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Down Long Click Start");
}



void down_long_click_stop_handler(ClickRecognizerRef recognizer, void *context) {
	new_command_issued = true;
	new_command = DOWN_LONG_CLICK_STOP;	

	APP_LOG(APP_LOG_LEVEL_INFO, "New Click: Down Long Click Stop");
}



void sensor_window_click_provider() {
	window_single_click_subscribe(BUTTON_ID_UP, up_click_handler);
	window_multi_click_subscribe(BUTTON_ID_UP, 2, 0, 0, true, up_double_click_handler);
	window_long_click_subscribe(BUTTON_ID_UP, 0, up_long_click_start_handler, up_long_click_stop_handler);

	window_single_click_subscribe(BUTTON_ID_DOWN, down_click_handler);
	window_multi_click_subscribe(BUTTON_ID_DOWN, 2, 0, 0, true, down_double_click_handler);
	window_long_click_subscribe(BUTTON_ID_DOWN, 0, down_long_click_start_handler, down_long_click_stop_handler);
}



static void new_text_layer(TextLayer **layer, Layer *root_layer, char *title, int16_t x, int16_t y, int16_t w, int16_t h) {
	*layer = text_layer_create(GRect(x, y, w, h));
	text_layer_set_text(*layer, title);

	text_layer_set_font(*layer, fonts_get_system_font(FONT_KEY_GOTHIC_18));
	text_layer_set_text_alignment(*layer, GTextAlignmentRight);

	layer_add_child(root_layer, text_layer_get_layer(*layer));
}



static void sensor_window_ui_load(Window *window) {
	Layer *root_layer = window_get_root_layer(window);
	GRect root_bound = layer_get_bounds(root_layer);

	new_text_layer
	(
		&up_button_text_layer,
		root_layer,		
		"UP",
		root_bound.size.w - 38,
		1,
		36,
		25
	);

	new_text_layer
	(
		&down_button_text_layer,
		root_layer,		
		"DOWN",
		root_bound.size.w - 38,
		root_bound.size.h - 26,
		36,
		25
	);

	new_text_layer
	(
		&status_text_layer,
		root_layer,
		"Connected",
		0,
		root_bound.size.h / 2 - 14,
		83,
		25
	);	
}



static void sensor_window_data_load(Window *window) {
	new_sensor_data = calloc(SAMPLES_PER_UPDATE, sizeof(int16_t) * 3);
	new_sensor_data_count = 0;	

	app_comm_set_sniff_interval(sniff_interval);

	app_message_register_outbox_failed(outbox_failed_callback);
	app_message_register_outbox_sent(outbox_sent_callback);

	accel_data_service_subscribe(SAMPLES_PER_UPDATE, sensor_data_handler);
	accel_service_set_sampling_rate(accel_sampling_rate);

	send_freq_data();

	window_set_click_config_provider(window, (ClickConfigProvider)sensor_window_click_provider);
	new_command_issued = false;
	new_command = 0;
}



static void sensor_window_load(Window *window) {
	sensor_window_ui_load(window);
	sensor_window_data_load(window);
}



static void sensor_window_unload(Window *window) {
	accel_data_service_unsubscribe();
	app_message_deregister_callbacks();	
	free(new_sensor_data);
}



static void bt_window_menu_callback(int index, void *context) {
	switch (index) {
		case 0:	
			sniff_interval = SNIFF_INTERVAL_REDUCED;
			break;
		case 1:
			sniff_interval = SNIFF_INTERVAL_NORMAL;
			break;
	}

	window_stack_pop(true);
}



static void bt_window_load(Window *window) {
	Layer *window_root_layer = window_get_root_layer(window);

	bt_window_menu_layer = simple_menu_layer_create(
		layer_get_bounds(window_root_layer),
		window,
		bt_menu_sections,
		1,
		NULL
	);

	switch (sniff_interval) {
		case SNIFF_INTERVAL_REDUCED:
			simple_menu_layer_set_selected_index(bt_window_menu_layer, 0, false);
			break;
		case SNIFF_INTERVAL_NORMAL:
			simple_menu_layer_set_selected_index(bt_window_menu_layer, 1, false);
			break;
	}	

	layer_add_child(window_root_layer, simple_menu_layer_get_layer(bt_window_menu_layer));
}



static void bt_window_unload(Window *window) {
	simple_menu_layer_destroy(bt_window_menu_layer);
}



static void freq_window_menu_callback(int index, void *context) {
	switch (index) {
		case 0:
			accel_sampling_rate = ACCEL_SAMPLING_10HZ;
			break;
		case 1:
			accel_sampling_rate = ACCEL_SAMPLING_25HZ;
			break;
		case 2:
			accel_sampling_rate = ACCEL_SAMPLING_50HZ;
			break;
		case 3:
			accel_sampling_rate = ACCEL_SAMPLING_100HZ;
			break;
	}

	window_stack_pop(true);
}



static void freq_window_load(Window *window) {
	Layer *window_root_layer = window_get_root_layer(window);

	freq_window_menu_layer = simple_menu_layer_create(
		layer_get_bounds(window_root_layer),
		window,
		freq_menu_sections,
		1,
		NULL
	);

	switch (accel_sampling_rate) {
		case ACCEL_SAMPLING_10HZ:
			simple_menu_layer_set_selected_index(freq_window_menu_layer, 0, false);
			break;
		case ACCEL_SAMPLING_25HZ:
			simple_menu_layer_set_selected_index(freq_window_menu_layer, 1, false);
			break;
		case ACCEL_SAMPLING_50HZ:
			simple_menu_layer_set_selected_index(freq_window_menu_layer, 2, false);
			break;
		case ACCEL_SAMPLING_100HZ:
			simple_menu_layer_set_selected_index(freq_window_menu_layer, 3, false);
			break;			
	}

	layer_add_child(window_root_layer, simple_menu_layer_get_layer(freq_window_menu_layer));
}



static void freq_window_unload(Window *window) {
	simple_menu_layer_destroy(freq_window_menu_layer);
}



static void main_window_menu_callback(int index, void *context) {
	switch (index)
	{
		case 0:
			window_stack_push(window_sensor, true);
			break;
		case 1:
			window_stack_push(window_freq, true);
			break;
		case 2:
			window_stack_push(window_bt, true);
			break;
	}
}



static void main_window_load(Window *window) {
	Layer *window_root_layer = window_get_root_layer(window);

	main_window_menu_layer = simple_menu_layer_create(
		layer_get_bounds(window_root_layer),
		window,
		main_menu_sections,
		1,
		NULL
	);

	layer_add_child(window_root_layer, simple_menu_layer_get_layer(main_window_menu_layer));
}



static void main_window_unload(Window *window) {
	simple_menu_layer_destroy(main_window_menu_layer);
}



void menu_items_setup() {
	freq_menu_items[0] = (SimpleMenuItem) { .title = "10Hz", .callback = freq_window_menu_callback };
	freq_menu_items[1] = (SimpleMenuItem) { .title = "25Hz", .callback = freq_window_menu_callback };
	freq_menu_items[2] = (SimpleMenuItem) { .title = "50Hz", .callback = freq_window_menu_callback };
	freq_menu_items[3] = (SimpleMenuItem) { .title = "100Hz", .callback = freq_window_menu_callback };

	freq_menu_sections[0] = (SimpleMenuSection)	{ .items = freq_menu_items, .num_items = 4 };	

	main_menu_items[0] = (SimpleMenuItem)
		{
			.title = "Start",
			.subtitle = "Start transmiting data",
			.callback = main_window_menu_callback
		};
	main_menu_items[1] = (SimpleMenuItem)
		{
			.title = "Frequency",
			.subtitle = "Sampling Frequency",
			.callback = main_window_menu_callback
		};
	main_menu_items[2] = (SimpleMenuItem)
		{
			.title = "Fast Transfer",
			.subtitle = "Increase Bluetooth Transmission Rate",
			.callback = main_window_menu_callback
		};

	main_menu_sections[0] = (SimpleMenuSection) { .items = main_menu_items, .num_items = 3 };

	bt_menu_items[0] = (SimpleMenuItem) { .title = "ON", .callback = bt_window_menu_callback };
	bt_menu_items[1] = (SimpleMenuItem) { .title = "OFF", .callback = bt_window_menu_callback };

	bt_menu_sections[0] = (SimpleMenuSection) { .items = bt_menu_items, .num_items = 2 };	
}



static void windows_setup() {
	window_main = window_create();
	window_set_window_handlers(window_main, (WindowHandlers) {
		.load = main_window_load,
		.unload = main_window_unload,
	});

	window_freq = window_create();
	window_set_window_handlers(window_freq, (WindowHandlers) {
		.load = freq_window_load,
		.unload = freq_window_unload,
	});	

	window_bt = window_create();
	window_set_window_handlers(window_bt, (WindowHandlers) {
		.load = bt_window_load,
		.unload = bt_window_unload,
	});

	window_sensor = window_create();
	window_set_window_handlers(window_sensor, (WindowHandlers) {
		.load = sensor_window_load,
		.unload = sensor_window_unload,
	});	
}



void params_setup() {
	accel_sampling_rate = ACCEL_SAMPLING_50HZ;
	sniff_interval = SNIFF_INTERVAL_REDUCED;
}



static void init() {
	menu_items_setup();
	params_setup();
	windows_setup();	

	app_message_open(app_message_inbox_size_maximum(), app_message_outbox_size_maximum());
	
	window_stack_push(window_main, true);
}



static void deinit() {
	window_destroy(window_main);
	window_destroy(window_freq);
	window_destroy(window_bt);
	window_destroy(window_sensor);
}



int main() {
	init();
	app_event_loop();
	deinit();
}
